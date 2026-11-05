namespace HuePilot.Application.Hue;

/// <summary>
/// Coordinates intent parsing, resource resolution, action validation, and the separate execution step. Planning
/// never changes Hue state; the UI can display the interpretation before calling <see cref="ExecuteAsync" />.
/// </summary>
public sealed class HueConversationService(IIntentParser parser, IHueService hueService)
{
    /// <summary>Builds a safe action proposal or a clarification response for a chat message.</summary>
    /// <param name="request">The latest user request.</param>
    /// <param name="history">Previous turns in the current conversation.</param>
    /// <param name="cancellationToken">Cancels resource lookup or intent parsing.</param>
    /// <returns>A displayable plan; a proposed action has not yet been executed.</returns>
    public async Task<ConversationPlan> PlanAsync(string request, IReadOnlyList<ChatTurn> history,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request))
        {
            return new ConversationPlan("Please enter a request.", NeedsClarification: true);
        }

        var resources = await hueService.GetResourcesAsync(cancellationToken);
        var intents = await parser.ParseAsync(request, history, resources, cancellationToken);
        if (intents.Count == 0)
        {
            return new ConversationPlan("I couldn't determine the requested action. Could you rephrase it?",
                NeedsClarification: true);
        }

        var actions = new List<HueAction>();
        var messages = new List<string>();
        foreach (var proposedIntent in intents)
        {
            var resolved = await ResolveIntentAsync(proposedIntent, request, resources, cancellationToken);
            if (resolved.NeedsClarification)
            {
                return resolved;
            }

            if (resolved.ProposedAction is not null)
            {
                actions.Add(resolved.ProposedAction);
            }
            else
            {
                messages.Add(resolved.AssistantMessage);
            }
        }

        if (actions.Count > 0)
        {
            return new ConversationPlan(
                messages.Count > 0
                    ? string.Join(" ", messages.Concat([DescribeActions(actions)]))
                    : DescribeActions(actions), actions[0], ProposedActions: actions);
        }

        return new ConversationPlan(string.Join(" ", messages));
    }

    private async Task<ConversationPlan> ResolveIntentAsync(HueIntent intent, string request,
        IReadOnlyList<HueResource> resources, CancellationToken cancellationToken)
    {
        if (intent.NeedsClarification)
        {
            return new ConversationPlan(intent.ClarificationQuestion!, NeedsClarification: true);
        }

        if (intent.Operation == HueOperation.ListResources)
        {
            return new ConversationPlan(DescribeResources(intent, resources));
        }

        if (intent.Operation is null || string.IsNullOrWhiteSpace(intent.TargetName) ||
            string.IsNullOrWhiteSpace(intent.TargetType))
        {
            return new ConversationPlan("I couldn't determine the action and target. Could you rephrase your request?",
                NeedsClarification: true);
        }

        var matches = resources.Where(resource =>
            resource.Type.Equals(intent.TargetType, StringComparison.OrdinalIgnoreCase) &&
            resource.Name.Equals(intent.TargetName, StringComparison.OrdinalIgnoreCase)).ToArray();

        // Trust room names only when they also appear in a discovered association; this grounds
        // duplicate scene/light selection in Bridge data instead of relying on a model guess.
        var explicitGroups =
            intent.TargetType is not null && (intent.TargetType.Equals(HueResourceTypes.Room, StringComparison.OrdinalIgnoreCase) ||
                                              intent.TargetType.Equals(HueResourceTypes.Zone, StringComparison.OrdinalIgnoreCase))
                ? []
                : resources.SelectMany(resource => resource.Groups ?? []).DistinctBy(group => group.Id)
                    .Where(group => ContainsWholePhrase(request, group.Name)).ToArray();

        if (explicitGroups.Length > 1)
        {
            return new ConversationPlan(
                $"The request mentions multiple rooms or zones: {string.Join(", ", explicitGroups.Select(group => group.Name).Distinct(StringComparer.OrdinalIgnoreCase))}. Which one should I use?",
                NeedsClarification: true);
        }

        if (explicitGroups.Length == 1 && !string.IsNullOrWhiteSpace(intent.TargetGroupName) && !explicitGroups[0].Name
                .Equals(intent.TargetGroupName, StringComparison.OrdinalIgnoreCase))
        {
            return new ConversationPlan(
                $"The request mentions {explicitGroups[0].Name}, but the proposed resource is associated with {intent.TargetGroupName}. Which room or group did you mean?",
                NeedsClarification: true);
        }

        var requestedGroupName = intent.TargetGroupName ?? (explicitGroups.Length == 1 ? explicitGroups[0].Name : null);
        var requestedGroupType = intent.TargetGroupType ?? (explicitGroups.Length == 1 ? explicitGroups[0].Type : null);
        if (!string.IsNullOrWhiteSpace(requestedGroupName) &&
            !intent.TargetType!.Equals(HueResourceTypes.Room, StringComparison.OrdinalIgnoreCase) &&
            !intent.TargetType.Equals(HueResourceTypes.Zone, StringComparison.OrdinalIgnoreCase))
        {
            matches = matches.Where(resource => (resource.Groups ?? []).Any(group =>
                group.Name.Equals(requestedGroupName, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(requestedGroupType) ||
                 group.Type.Equals(requestedGroupType, StringComparison.OrdinalIgnoreCase)))).ToArray();
            if (matches.Length == 0)
            {
                return new ConversationPlan(
                    $"I couldn't find a {intent.TargetType} named ‘{intent.TargetName}’ associated with {requestedGroupType ?? "group"} ‘{requestedGroupName}’. Which room or scene did you mean?",
                    NeedsClarification: true);
            }
        }

        if (matches.Length == 0)
        {
            return new ConversationPlan(
                $"I couldn't find a {intent.TargetType} named ‘{intent.TargetName}’. Which room, zone, light, or scene did you mean?",
                NeedsClarification: true);
        }

        if (matches.Length > 1)
        {
            return new ConversationPlan(DescribeAmbiguousMatches(intent.TargetType, intent.TargetName, matches),
                NeedsClarification: true);
        }

        var target = matches[0];

        if (intent.Operation == HueOperation.GetStatus)
        {
            if (!target.Type.Equals(HueResourceTypes.Light, StringComparison.OrdinalIgnoreCase) &&
                !target.Type.Equals(HueResourceTypes.Room, StringComparison.OrdinalIgnoreCase) &&
                !target.Type.Equals(HueResourceTypes.Zone, StringComparison.OrdinalIgnoreCase))
            {
                return new ConversationPlan("I can report the status of a light, room, or zone, but not a scene.",
                    NeedsClarification: true);
            }

            var status = await hueService.GetStatusAsync(target, cancellationToken);
            return new ConversationPlan(status);
        }

        if (!HueActionValidator.TryCreate(intent, target, out var action, out var validationError))
        {
            return new ConversationPlan(validationError, NeedsClarification: true);
        }

        return new ConversationPlan(Describe(action!), action);
    }

    private static string DescribeResources(HueIntent intent, IReadOnlyList<HueResource> resources)
    {
        var desiredType = intent.TargetType?.Trim().ToLowerInvariant() switch
        {
            HueResourceTypes.Room => HueResourceTypes.Room,
            HueResourceTypes.Scene => HueResourceTypes.Scene,
            HueResourceTypes.Zone => HueResourceTypes.Zone,
            _ => HueResourceTypes.Light
        };
        var groupName = intent.TargetGroupName?.Trim();
        var nameFilter = intent.TargetName?.Trim();

        var matches = resources.Where(resource =>
        {
            if (!resource.Type.Equals(desiredType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var requestedGroup = !string.IsNullOrWhiteSpace(groupName) ? groupName : nameFilter;
            if (string.IsNullOrWhiteSpace(requestedGroup))
            {
                return true;
            }

            if (desiredType == HueResourceTypes.Room)
            {
                return resource.Name.Equals(requestedGroup, StringComparison.OrdinalIgnoreCase);
            }

            return (resource.Groups ?? []).Any(group =>
                group.Name.Equals(requestedGroup, StringComparison.OrdinalIgnoreCase));
        }).ToArray();

        var typeLabel = desiredType switch
        {
            HueResourceTypes.Room => "Rooms",
            HueResourceTypes.Scene => "Scenes",
            HueResourceTypes.Zone => "Zones",
            _ => "Lights"
        };
        var scope = !string.IsNullOrWhiteSpace(groupName) ? $" in {groupName}" :
            !string.IsNullOrWhiteSpace(nameFilter) && !matches.Any(resource =>
                resource.Name.Equals(nameFilter, StringComparison.OrdinalIgnoreCase)) ? $" in {nameFilter}" : "";
        return matches.Length == 0
            ? $"No {typeLabel.ToLowerInvariant()} were found{scope}."
            : $"{typeLabel}{scope}: {string.Join(", ", matches.Select(resource => resource.Name))}.";
    }

    /// <summary>Executes an action returned by <see cref="PlanAsync" /> after the UI has shown it.</summary>
    /// <param name="action">The validated action to execute.</param>
    /// <param name="cancellationToken">Cancels the Bridge request.</param>
    public async Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
    {
        await hueService.ExecuteAsync(action, cancellationToken);
    }

    private static string DescribeActions(IReadOnlyList<HueAction> actions)
    {
        return actions.Count == 1
            ? Describe(actions[0])
            : $"Applying {actions.Count} requested Hue actions: {string.Join("; ", actions.Select(Describe))}";
    }

    private static string Describe(HueAction action)
    {
        return action.Operation switch
        {
            HueOperation.TurnOn => $"Turning on {action.Target.Name}.",
            HueOperation.TurnOff => $"Turning off {action.Target.Name}.",
            HueOperation.SetBrightness => $"Setting {action.Target.Name} to {action.BrightnessPercent}% brightness.",
            HueOperation.SetColor => $"Setting {action.Target.Name} to {action.Color}.",
            HueOperation.ActivateScene => DescribeScene(action.Target),
            _ => "Applying the requested change."
        };
    }

    private static string DescribeScene(HueResource scene)
    {
        var rooms = (scene.Groups ?? []).Where(group => group.Type.Equals(HueResourceTypes.Room,
            StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return rooms.Length == 1
            ? $"Activating the {scene.Name} scene in {rooms[0].Name}."
            : $"Activating the {scene.Name} scene.";
    }

    private static string DescribeAmbiguousMatches(string? type, string? name, IReadOnlyList<HueResource> matches)
    {
        var groups = matches.SelectMany(resource => resource.Groups ?? [])
            .Select(group => $"{group.Name} ({group.Type})").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return groups.Length > 0
            ? $"I found more than one {type} named ‘{name}’, associated with {string.Join(", ", groups)}. Which room or group should I use?"
            : $"I found more than one {type} named ‘{name}’. Please specify which one you mean.";
    }

    private static bool ContainsWholePhrase(string text, string phrase)
    {
        var index = 0;
        while ((index = text.IndexOf(phrase, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeIsBoundary = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var end = index + phrase.Length;
            var afterIsBoundary = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (beforeIsBoundary && afterIsBoundary)
            {
                return true;
            }

            index = end;
        }

        return false;
    }
}

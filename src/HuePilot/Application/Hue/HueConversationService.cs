using HuePilot.Application.Chat;
using HuePilot.Application.Hue.Models;

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
            return new ConversationPlan("Please enter a request.", true);
        }

        var resources = await hueService.GetResourcesAsync(cancellationToken);
        var intents = await parser.ParseAsync(request, history, resources, cancellationToken);
        if (intents.Count == 0)
        {
            return new ConversationPlan("I couldn't determine the requested action. Could you rephrase it?", true);
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

            if (resolved.ProposedActions is not null)
            {
                actions.AddRange(resolved.ProposedActions);
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
                    : DescribeActions(actions), ProposedActions: actions);
        }

        return new ConversationPlan(string.Join(" ", messages));
    }

    private async Task<ConversationPlan> ResolveIntentAsync(HueIntent intent, string request,
        IReadOnlyList<HueResource> resources, CancellationToken cancellationToken)
    {
        if (intent.NeedsClarification)
        {
            return new ConversationPlan(intent.ClarificationQuestion!, true);
        }

        if (intent is ListResourcesIntent)
        {
            return new ConversationPlan(DescribeResources(intent, resources));
        }

        if (string.IsNullOrWhiteSpace(intent.TargetName) || intent.TargetType is null)
        {
            return new ConversationPlan("I couldn't determine the action and target. Could you rephrase your request?",
                true);
        }

        var matches = resources.Where(resource =>
            resource.Type == intent.TargetType.Value &&
            resource.Name.Equals(intent.TargetName, StringComparison.OrdinalIgnoreCase)).ToArray();

        // Trust room names only when they also appear in a discovered association; this grounds
        // duplicate scene/light selection in Bridge data instead of relying on a model guess.
        var explicitGroups =
            intent.TargetType is HueResourceType.Room or HueResourceType.Zone
                ? []
                : resources.SelectMany(resource => resource.Groups ?? []).DistinctBy(group => group.Id)
                    .Where(group => ContainsWholePhrase(request, group.Name)).ToArray();

        if (explicitGroups.Length > 1)
        {
            return new ConversationPlan(
                $"The request mentions multiple rooms or zones: {string.Join(", ", explicitGroups.Select(group => group.Name).Distinct(StringComparer.OrdinalIgnoreCase))}. Which one should I use?",
                true);
        }

        if (explicitGroups.Length == 1 && !string.IsNullOrWhiteSpace(intent.TargetGroupName) && !explicitGroups[0].Name
                .Equals(intent.TargetGroupName, StringComparison.OrdinalIgnoreCase))
        {
            return new ConversationPlan(
                $"The request mentions {explicitGroups[0].Name}, but the proposed resource is associated with {intent.TargetGroupName}. Which room or group did you mean?",
                true);
        }

        var requestedGroupName = intent.TargetGroupName ?? (explicitGroups.Length == 1 ? explicitGroups[0].Name : null);
        var requestedGroupType = intent.TargetGroupType ??
            (explicitGroups.Length == 1 ? explicitGroups[0].Type : null);
        if (!string.IsNullOrWhiteSpace(requestedGroupName) &&
            intent.TargetType is not HueResourceType.Room and not HueResourceType.Zone)
        {
            matches = matches.Where(resource => (resource.Groups ?? []).Any(group =>
                group.Name.Equals(requestedGroupName, StringComparison.OrdinalIgnoreCase) &&
                (requestedGroupType is null || group.Type == requestedGroupType.Value))).ToArray();
            if (matches.Length == 0)
            {
                return new ConversationPlan(
                $"I couldn't find a {intent.TargetType.Value.ToHueV2WireType()} named ‘{intent.TargetName}’ associated with {requestedGroupType?.ToHueV2WireType() ?? "group"} ‘{requestedGroupName}’. Which room or scene did you mean?",
                    true);
            }
        }

        if (matches.Length == 0)
        {
            return new ConversationPlan(
                $"I couldn't find a {intent.TargetType.Value.ToHueV2WireType()} named ‘{intent.TargetName}’. Which room, zone, light, or scene did you mean?",
                true);
        }

        if (matches.Length > 1)
        {
            return new ConversationPlan(DescribeAmbiguousMatches(intent.TargetType.Value.ToHueV2WireType(), intent.TargetName, matches), true);
        }

        var target = matches[0];

        if (intent is GetStatusIntent)
        {
            if (target.Type is not (HueResourceType.Light or HueResourceType.Room or HueResourceType.Zone))
            {
                return new ConversationPlan("I can report the status of a light, room, or zone, but not a scene.",
                    true);
            }

            var status = await hueService.GetStatusAsync(target, cancellationToken);
            var brightness = status.BrightnessPercent is null
                ? string.Empty
                : $" at {status.BrightnessPercent}% brightness";
            var color = status.ColorX is null || status.ColorY is null
                ? string.Empty
                : $" with xy ({status.ColorX:F4}, {status.ColorY:F4})";
            return new ConversationPlan($"{status.ResourceName} is {(status.IsOn ? "on" : "off")}{brightness}{color}.");
        }

        if (!HueActionValidator.TryCreate(intent, target, out var action, out var validationError))
        {
            return new ConversationPlan(validationError, true);
        }

        return new ConversationPlan(Describe(action!), false, [action!]);
    }

    private static string DescribeResources(HueIntent query, IReadOnlyList<HueResource> resources)
    {
        var desiredType = query.TargetType switch
        {
            HueResourceType.Room => HueResourceType.Room,
            HueResourceType.Scene => HueResourceType.Scene,
            HueResourceType.Zone => HueResourceType.Zone,
            _ => HueResourceType.Light
        };
        var groupName = query.TargetGroupName?.Trim();
        var nameFilter = query.TargetName?.Trim();

        var matches = resources.Where(resource =>
        {
            if (resource.Type != desiredType)
            {
                return false;
            }

            var requestedGroup = !string.IsNullOrWhiteSpace(groupName) ? groupName : nameFilter;
            if (string.IsNullOrWhiteSpace(requestedGroup))
            {
                return true;
            }

            if (desiredType == HueResourceType.Room)
            {
                return resource.Name.Equals(requestedGroup, StringComparison.OrdinalIgnoreCase);
            }

            return (resource.Groups ?? []).Any(group =>
                group.Name.Equals(requestedGroup, StringComparison.OrdinalIgnoreCase));
        }).ToArray();

        var typeLabel = desiredType switch
        {
            HueResourceType.Room => "Rooms",
            HueResourceType.Scene => "Scenes",
            HueResourceType.Zone => "Zones",
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
        var rooms = (scene.Groups ?? []).Where(group => group.Type == HueResourceType.Room).ToArray();
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

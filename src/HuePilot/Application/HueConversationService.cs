namespace HuePilot.Application;

/// <summary>
/// Coordinates intent parsing, resource resolution, action validation, and the separate execution step.
/// Planning never changes Hue state; the UI can display the interpretation before calling <see cref="ExecuteAsync"/>.
/// </summary>
public sealed class HueConversationService(IIntentParser parser, IHueService hueService)
{
    /// <summary>Builds a safe action proposal or a clarification response for a chat message.</summary>
    /// <param name="request">The latest user request.</param>
    /// <param name="history">Previous turns in the current conversation.</param>
    /// <param name="cancellationToken">Cancels resource lookup or intent parsing.</param>
    /// <returns>A displayable plan; a proposed action has not yet been executed.</returns>
    public async Task<ConversationPlan> PlanAsync(
        string request,
        IReadOnlyList<ChatTurn> history,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request))
            return new("Please enter a request.", NeedsClarification: true);

        var resources = await hueService.GetResourcesAsync(cancellationToken);
        var intent = await parser.ParseAsync(request, history, resources, cancellationToken);

        if (intent.NeedsClarification)
            return new(intent.ClarificationQuestion!, NeedsClarification: true);

        if (intent.Operation is null || string.IsNullOrWhiteSpace(intent.TargetName) || string.IsNullOrWhiteSpace(intent.TargetType))
            return new("I couldn't determine the action and target. Could you rephrase your request?", NeedsClarification: true);

        var matches = resources.Where(resource =>
            resource.Type.Equals(intent.TargetType, StringComparison.OrdinalIgnoreCase) &&
            resource.Name.Equals(intent.TargetName, StringComparison.OrdinalIgnoreCase)).ToArray();

        // Trust room names only when they also appear in a discovered association; this grounds
        // duplicate scene/light selection in Bridge data instead of relying on a model guess.
        var explicitGroups = intent.TargetType.Equals("room", StringComparison.OrdinalIgnoreCase)
            ? []
            : resources.SelectMany(resource => resource.Groups ?? [])
                .DistinctBy(group => group.Id)
                .Where(group => ContainsWholePhrase(request, group.Name))
                .ToArray();

        if (explicitGroups.Length > 1)
            return new($"The request mentions multiple rooms or zones: {string.Join(", ", explicitGroups.Select(group => group.Name).Distinct(StringComparer.OrdinalIgnoreCase))}. Which one should I use?", NeedsClarification: true);

        if (explicitGroups.Length == 1 && !string.IsNullOrWhiteSpace(intent.TargetGroupName) &&
            !explicitGroups[0].Name.Equals(intent.TargetGroupName, StringComparison.OrdinalIgnoreCase))
            return new($"The request mentions {explicitGroups[0].Name}, but the proposed resource is associated with {intent.TargetGroupName}. Which room or group did you mean?", NeedsClarification: true);

        var requestedGroupName = intent.TargetGroupName ?? (explicitGroups.Length == 1 ? explicitGroups[0].Name : null);
        var requestedGroupType = intent.TargetGroupType ?? (explicitGroups.Length == 1 ? explicitGroups[0].Type : null);
        if (!string.IsNullOrWhiteSpace(requestedGroupName) && !intent.TargetType.Equals("room", StringComparison.OrdinalIgnoreCase))
        {
            matches = matches.Where(resource => (resource.Groups ?? [])
                .Any(group => group.Name.Equals(requestedGroupName, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(requestedGroupType) || group.Type.Equals(requestedGroupType, StringComparison.OrdinalIgnoreCase))))
                .ToArray();
            if (matches.Length == 0)
                return new($"I couldn't find a {intent.TargetType} named ‘{intent.TargetName}’ associated with {requestedGroupType ?? "group"} ‘{requestedGroupName}’. Which room or scene did you mean?", NeedsClarification: true);
        }

        if (matches.Length == 0)
            return new($"I couldn't find a {intent.TargetType} named ‘{intent.TargetName}’. Which room, light, or scene did you mean?", NeedsClarification: true);

        if (matches.Length > 1)
            return new(DescribeAmbiguousMatches(intent.TargetType, intent.TargetName, matches), NeedsClarification: true);

        var target = matches[0];

        if (!HueActionValidator.TryCreate(intent, target, out var action, out var validationError))
            return new(validationError, NeedsClarification: true);

        return new(Describe(action!), action);
    }

    /// <summary>Executes an action returned by <see cref="PlanAsync"/> after the UI has shown it.</summary>
    /// <param name="action">The validated action to execute.</param>
    /// <param name="cancellationToken">Cancels the Bridge request.</param>
    public async Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default) =>
        await hueService.ExecuteAsync(action, cancellationToken);

    private static string Describe(HueAction action) => action.Operation switch
    {
        HueOperation.TurnOn => $"Turning on {action.Target.Name}.",
        HueOperation.TurnOff => $"Turning off {action.Target.Name}.",
        HueOperation.SetBrightness => $"Setting {action.Target.Name} to {action.BrightnessPercent}% brightness.",
        HueOperation.SetColor => $"Setting {action.Target.Name} to {action.Color}.",
        HueOperation.ActivateScene => DescribeScene(action.Target),
        _ => "Applying the requested change."
    };

    private static string DescribeScene(HueResource scene)
    {
        var rooms = (scene.Groups ?? []).Where(group => group.Type.Equals("room", StringComparison.OrdinalIgnoreCase)).ToArray();
        return rooms.Length == 1
            ? $"Activating the {scene.Name} scene in {rooms[0].Name}."
            : $"Activating the {scene.Name} scene.";
    }

    private static string DescribeAmbiguousMatches(string? type, string? name, IReadOnlyList<HueResource> matches)
    {
        var groups = matches.SelectMany(resource => resource.Groups ?? [])
            .Select(group => $"{group.Name} ({group.Type})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
            if (beforeIsBoundary && afterIsBoundary) return true;
            index = end;
        }
        return false;
    }
}

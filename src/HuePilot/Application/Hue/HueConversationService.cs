using System.Text.RegularExpressions;
using HuePilot.Application.Chat;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;
using HuePilot.Application.IntentParsing;

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

        var hasMutation = intents.Any(intent =>
            intent is TurnOnIntent or TurnOffIntent or SetBrightnessIntent or SetColorIntent or ActivateSceneIntent);
        if (hasMutation && intents.OfType<ListResourcesIntent>().Any(query =>
                query.TargetGroupId is null && !RequestsGlobalList(request, query.ResourceType)))
        {
            return new ConversationPlan("Which room or zone should I list before making the requested change?", true);
        }

        var literalIds = HueRequestIds.Find(request);
        var requestedId = intents switch
        {
            [TargetedHueIntent command] => command.TargetResourceId,
            [ListResourcesIntent list] => list.TargetGroupId,
            _ => null
        };
        if (literalIds.Length > 0 && (literalIds.Length > 1 || literalIds[0] != requestedId))
        {
            return new ConversationPlan("Please use one matching resource ID and one action per request.", true);
        }

        var groups = resources.OfType<HueResourceGroup>().ToArray();
        var actions = new List<HueAction>();
        var messages = new List<string>();
        foreach (var proposedIntent in intents)
        {
            var resolved = await PlanIntentAsync(proposedIntent, resources, cancellationToken);
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
            return new ConversationPlan(string.Join(" ", messages.Append(DescribeActions(actions, groups))),
                ProposedActions: actions);
        }

        return new ConversationPlan(string.Join(" ", messages));
    }

    private static bool RequestsGlobalList(string request, HueResourceType type)
    {
        var category = type switch
        {
            HueResourceType.Light => "(?:lights|lamps|lichter|lampen)",
            HueResourceType.Room => "(?:rooms|räume)",
            HueResourceType.Zone => "(?:zones|zonen)",
            HueResourceType.Scene => "(?:scenes|szenen)",
            _ => null
        };
        if (category is null)
        {
            return false;
        }

        // A conjunction or comma starts a separate clause only if another command follows.
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        const string nextCommand =
            @"(?=(?:(?:please|bitte)\s+)?(?:turn|switch|set|dim|activate|list|show|schalte|setze|aktiviere|liste|zeige)\b)";
        var clauses = Regex.Split(request,
            @"(?:\b(?:and(?:\s+then)?|then|und(?:\s+dann)?|dann)\b|[,;.!?])\s*" + nextCommand, options);
        var globalList =
            $@"^\s*(?:(?:please|bitte)\s+)?(?:list|show|liste|zeige)\s+(?:all\s+(?:the\s+)?|alle\s+){category}(?:\s+auf)?\s*[.?!]?\s*$";
        return clauses.Any(clause => Regex.IsMatch(clause, globalList, options));
    }

    private async Task<ConversationPlan> PlanIntentAsync(HueIntent intent, IReadOnlyList<HueResource> resources,
        CancellationToken cancellationToken)
    {
        if (intent is ClarificationIntent clarification)
        {
            return new ConversationPlan(clarification.Question, true);
        }

        var groupId = intent switch
        {
            TargetedHueIntent command => command.TargetGroupId,
            ListResourcesIntent list => list.TargetGroupId,
            _ => null
        };
        var group = groupId is { } id ? HueResource.FindUniqueById(resources, id) as HueResourceGroup : null;
        if (groupId is not null && (group is null || group.Type is not (HueResourceType.Room or HueResourceType.Zone)))
        {
            return new ConversationPlan("I couldn't match that ID to a unique room or zone. Which one did you mean?",
                true);
        }

        if (intent is ListResourcesIntent query)
        {
            return PlanResourceList(query.ResourceType, resources, group);
        }

        if (intent is not TargetedHueIntent targeted ||
            HueResource.FindUniqueById(resources, targeted.TargetResourceId) is not { } target)
        {
            return new ConversationPlan("I couldn't match that ID to a unique Hue resource. Which one did you mean?",
                true);
        }

        if (group is not null && !group.ContainsResource(target.Id))
        {
            return new ConversationPlan($"That resource is not associated with {group.Name}. Which one did you mean?",
                true);
        }

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

        if (!HueActionValidator.TryCreate(targeted, target, out var action, out var validationError))
        {
            return new ConversationPlan(validationError, true);
        }

        return new ConversationPlan("", ProposedActions: [action!]);
    }

    private static ConversationPlan PlanResourceList(HueResourceType type, IReadOnlyList<HueResource> resources,
        HueResourceGroup? group)
    {
        if (type is not (HueResourceType.Room or HueResourceType.Zone or HueResourceType.Light
            or HueResourceType.Scene))
        {
            return new ConversationPlan("Which Hue resource category should I list?", true);
        }

        var matches = resources.Where(resource => resource.Type == type &&
                                                  (group is null || ReferenceEquals(resource, group) ||
                                                   group.ContainsResource(resource.Id)));
        var names = matches.Select(resource => resource.Name).ToArray();
        var scope = group is null || group.Type == type ? "" : $" in {group.Name}";
        return new ConversationPlan(names.Length == 0
            ? $"No {type.ToHueV2WireType()}s were found{scope}."
            : $"{type}s{scope}: {string.Join(", ", names)}.");
    }

    /// <summary>Executes an action returned by <see cref="PlanAsync" /> after the UI has shown it.</summary>
    /// <param name="action">The validated action to execute.</param>
    /// <param name="cancellationToken">Cancels the Bridge request.</param>
    public async Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
    {
        await hueService.ExecuteAsync(action, cancellationToken);
    }

    private static string DescribeActions(IReadOnlyList<HueAction> actions, IReadOnlyList<HueResourceGroup> groups)
    {
        return actions.Count == 1
            ? Describe(actions[0], groups)
            : $"Applying {actions.Count} requested Hue actions: {string.Join("; ", actions.Select(action => Describe(action, groups)))}";
    }

    private static string Describe(HueAction action, IReadOnlyList<HueResourceGroup> groups)
    {
        return action.Operation switch
        {
            HueOperation.TurnOn => $"Turning on {action.Target.Name}.",
            HueOperation.TurnOff => $"Turning off {action.Target.Name}.",
            HueOperation.SetBrightness => $"Setting {action.Target.Name} to {action.BrightnessPercent}% brightness.",
            HueOperation.SetColor => $"Setting {action.Target.Name} to {action.Color}.",
            HueOperation.ActivateScene => DescribeScene(action.Target, groups),
            _ => "Applying the requested change."
        };
    }

    private static string DescribeScene(HueResource scene, IReadOnlyList<HueResourceGroup> groups)
    {
        var owners = groups.Where(group => group.ContainsResource(scene.Id)).ToArray();
        return owners.Length == 1
            ? $"Activating the {scene.Name} scene in {owners[0].Name}."
            : $"Activating the {scene.Name} scene.";
    }
}
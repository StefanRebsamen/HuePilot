using System.Text.RegularExpressions;
using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;

namespace HuePilot.Application.IntentParsing;

/// <summary>
/// Grounds untrusted provider proposals in the current Hue catalog before they become application intents.
/// </summary>
/// <remarks>
/// Provider-supplied names and IDs are never executable authority. This resolver checks that an ID uniquely identifies
/// a discovered resource, validates any name/type claim against that resource, confirms optional group membership, and
/// returns a <see cref="ClarificationIntent" /> whenever a selection is unknown or ambiguous. Both OpenAI parser
/// approaches use this policy so they produce the same ID-first intent model.
/// </remarks>
/// <param name="resources">The server-side catalog discovered from the Hue Bridge for this request.</param>
/// <param name="request">The raw user request, used only to corroborate explicitly mentioned groups and IDs.</param>
internal sealed class HueIntentResolver(IReadOnlyList<HueResource> resources, string request)
{
    // Groups are materialized once because membership validation occurs repeatedly while resolving a proposal batch.
    private readonly HueResourceGroup[] _groups = resources.OfType<HueResourceGroup>().ToArray();


    // Named groups in the user message are retained to prevent the provider from silently choosing one of several scopes.
    private readonly HashSet<string> _mentionedGroups = resources.OfType<HueResourceGroup>()
        .Where(group => group.Name.Length > 0 && Regex.IsMatch(request,
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(group.Name)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)).Select(group => group.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ensures a compound request covers every explicitly named room or zone.</summary>
    /// <param name="intents">The catalog-grounded intents proposed for the request.</param>
    /// <returns>The intents, or one clarification when group selection is incomplete.</returns>
    internal IReadOnlyList<HueIntent> CheckCompleteGroupSelection(IReadOnlyList<HueIntent> intents)
    {
        if (intents.OfType<ClarificationIntent>().FirstOrDefault() is { } clarification)
        {
            return [clarification];
        }

        if (_mentionedGroups.Count < 2)
        {
            return intents;
        }

        var targetedGroups = intents.OfType<TargetedHueIntent>()
            .Select(intent => HueResource.FindUniqueById(resources, intent.TargetResourceId)).OfType<HueResourceGroup>()
            .Select(group => group.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return targetedGroups.Count > 0 && !_mentionedGroups.SetEquals(targetedGroups)
            ? [new ClarificationIntent("Which of the rooms or zones you mentioned should I use?")]
            : intents;
    }

    /// <summary>Validates one untrusted provider proposal and converts it to an ID-grounded intent.</summary>
    /// <param name="proposal">The provider proposal containing names, IDs, and requested values.</param>
    /// <param name="requireLookupId">Whether a target ID must have come from a tool lookup.</param>
    /// <returns>A resolved intent or a clarification when the proposal is unsafe or ambiguous.</returns>
    internal HueIntent Resolve(HueIntentProposal proposal, bool requireLookupId)
    {
        if (!string.IsNullOrWhiteSpace(proposal.ClarificationQuestion) || proposal.Operation is null)
        {
            return new ClarificationIntent(string.IsNullOrWhiteSpace(proposal.ClarificationQuestion)
                ? "What Hue action would you like?"
                : proposal.ClarificationQuestion);
        }

        var isList = proposal.Operation == HueOperation.ListResources;
        var identified = proposal.TargetId is { } targetId ? HueResource.FindUniqueById(resources, targetId) : null;
        if (identified is null && (proposal.TargetId is not null || (requireLookupId && !isList)))
        {
            return new ClarificationIntent(
                "I couldn't match that ID to a unique Hue resource. Which one did you mean?");
        }

        if (identified is not null && !string.IsNullOrWhiteSpace(proposal.TargetName) &&
            !Same(identified.Name, proposal.TargetName))
        {
            return new ClarificationIntent("The resource name and ID do not match. Which one did you mean?");
        }

        var type = identified?.Type;
        if (proposal.TargetType is not null)
        {
            if (!HueResourceTypeExtensions.TryParseHueV2WireType(proposal.TargetType, out var parsed) ||
                (!isList && identified is not null && identified.Type != parsed))
            {
                return new ClarificationIntent("Which resource type did you mean?");
            }

            type = parsed;
        }

        if (type is not (HueResourceType.Room or HueResourceType.Zone or HueResourceType.Light
            or HueResourceType.Scene))
        {
            return new ClarificationIntent("Which Hue resource category did you mean?");
        }

        var name = identified?.Name ?? proposal.TargetName;
        var targetsGroup = !isList && type is HueResourceType.Room or HueResourceType.Zone;
        var scopedId = proposal.TargetGroupId;
        var scopedName = proposal.TargetGroupName;
        if (targetsGroup || (isList && (identified is HueResourceGroup || identified is null)))
        {
            if ((scopedName is not null && name is not null && !Same(scopedName, name)) ||
                (scopedId is not null && identified is not null && scopedId != identified.Id))
            {
                return new ClarificationIntent("The proposed groups conflict. Which room or zone did you mean?");
            }

            scopedName ??= name;
            scopedId ??= identified?.Id;
        }


        if (isList && identified is null && scopedId is null && string.IsNullOrWhiteSpace(scopedName) &&
            proposal.TargetGroupType is null)
        {
            return new ListResourcesIntent(type.Value);
        }

        var (group, error) = ResolveGroup(scopedName, scopedId, proposal.TargetGroupType, targetsGroup);
        if (error is not null)
        {
            return error;
        }

        if (isList)
        {
            if (identified is not null && !ReferenceEquals(identified, group) &&
                (group is null || !group.ContainsResource(identified.Id)))
            {
                return new ClarificationIntent("Which room or zone should I list?");
            }

            return new ListResourcesIntent(type.Value, group?.Id);
        }

        return ResolveCommand(proposal, type.Value, name, identified, group, targetsGroup);
    }

    /// <summary>Resolves an allowlisted operation to one exact catalog resource and optional containing group.</summary>
    private HueIntent ResolveCommand(HueIntentProposal proposal, HueResourceType type, string? name,
        HueResource? identified, HueResourceGroup? group, bool targetsGroup)
    {
        var candidates = resources.Where(resource => resource.Type == type && Same(resource.Name, name)).ToArray();

        // The provider can confirm membership, but only the user's selection can distinguish two same-named lights.
        if (group is not null && (targetsGroup || candidates.Length <= 1 || _mentionedGroups.Contains(group.Name)))
        {
            candidates = candidates.Where(resource => targetsGroup
                ? ReferenceEquals(resource, group)
                : group.ContainsResource(resource.Id)).ToArray();
        }

        if (candidates.Length > 1)
        {
            var owners = _groups.Where(g => candidates.Any(r => g.ContainsResource(r.Id))).Select(g => g.Name)
                .Distinct();
            var options = string.Join(", ", owners);
            return new ClarificationIntent($"I found more than one {type.ToHueV2WireType()} named ‘{name}’. " +
                                           (options.Length == 0
                                               ? "Which one did you mean?"
                                               : $"Which group did you mean: {options}?"));
        }

        if (candidates is not [var target] ||
            !ReferenceEquals(target, HueResource.FindUniqueById(resources, target.Id)) ||
            (identified is not null && !ReferenceEquals(identified, target)))
        {
            var scope = group is null ? "" : $" associated with {group.Type.ToHueV2WireType()} ‘{group.Name}’";
            return new ClarificationIntent(
                $"I couldn't match ‘{name}’ to a unique resource{scope}. Which one did you mean?");
        }

        var groupId = targetsGroup ? null : group?.Id;
        TargetedHueIntent? intent = proposal.Operation switch
        {
            HueOperation.TurnOn => new TurnOnIntent(target.Id, groupId),
            HueOperation.TurnOff => new TurnOffIntent(target.Id, groupId),
            HueOperation.SetBrightness => new SetBrightnessIntent(target.Id, proposal.BrightnessPercent, groupId),
            HueOperation.SetColor => new SetColorIntent(target.Id, proposal.Color, groupId),
            HueOperation.ActivateScene => new ActivateSceneIntent(target.Id, groupId),
            HueOperation.GetStatus => new GetStatusIntent(target.Id, groupId),
            _ => null
        };
        if (intent is null)
        {
            return new ClarificationIntent("What supported Hue action would you like?");
        }

        return HueActionValidator.TryCreate(intent, target, out _, out var validationError)
            ? intent
            : new ClarificationIntent(validationError);
    }

    /// <summary>Finds one uniquely identified room or zone while rejecting conflicting name, type, or ID claims.</summary>
    private (HueResourceGroup? Group, ClarificationIntent? Error) ResolveGroup(string? name, Guid? id, string? type,
        bool directGroupTarget)
    {
        var identified = id is { } groupId ? HueResource.FindUniqueById(resources, groupId) as HueResourceGroup : null;
        if (id is not null && identified is null)
        {
            return (null, new ClarificationIntent("Which discovered room or zone did you mean?"));
        }

        if (_mentionedGroups.Count > 1 && (!directGroupTarget || name is null || !_mentionedGroups.Contains(name) ||
                                           Regex.IsMatch(request, @"\b(or|oder)\b",
                                               RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
        {
            return (null,
                new ClarificationIntent(
                    $"The request mentions multiple rooms or zones: {string.Join(", ", _mentionedGroups)}. Which one should I use?"));
        }

        var mentionedName = _mentionedGroups.Count == 1 ? _mentionedGroups.Single() : null;
        name = string.IsNullOrWhiteSpace(name) ? identified?.Name ?? mentionedName : name;
        if (name is null && type is null)
        {
            return (null, null);
        }

        var matches = _groups.Where(resourceGroup => Same(resourceGroup.Name, name)).ToArray();
        if (matches.Length > 1)
        {
            return (null,
                new ClarificationIntent($"I found more than one room or zone named ‘{name}’. Which one did you mean?"));
        }

        if (matches is not [var group] || !ReferenceEquals(HueResource.FindUniqueById(resources, group.Id), group) ||
            (identified is not null && !ReferenceEquals(identified, group)) ||
            (mentionedName is not null && !Same(mentionedName, name)) ||
            (type is not null && !Same(type, group.Type.ToHueV2WireType())))
        {
            return (null,
                new ClarificationIntent(
                    $"I couldn't match ‘{name}’ to the requested room or zone. Which one did you mean?"));
        }

        return (group, null);
    }

    /// <summary>Compares provider-facing names and wire types without treating case differences as distinct values.</summary>
    private static bool Same(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
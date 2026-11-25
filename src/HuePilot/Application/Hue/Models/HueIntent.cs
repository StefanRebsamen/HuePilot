namespace HuePilot.Application.Hue.Models;

/// <summary>
/// A model-produced interpretation of a user's request. It is untrusted until a discovered resource is resolved
/// and its operation and arguments are validated by the application.
/// </summary>
public abstract record HueIntent(
    string? TargetName,
    HueResourceType? TargetType,
    string? TargetGroupName = null,
    HueResourceType? TargetGroupType = null)
{
    /// <summary>Gets a follow-up question when the model cannot safely infer one action.</summary>
    public virtual string? ClarificationQuestion => null;

    /// <summary>Gets whether the model asked the user to clarify the request.</summary>
    public bool NeedsClarification => !string.IsNullOrWhiteSpace(ClarificationQuestion);
}

/// <summary>Requests user input before a Hue action can be safely proposed.</summary>
public sealed record ClarificationIntent(string Question) : HueIntent(null, null)
{
    /// <inheritdoc />
    public override string ClarificationQuestion => Question;
}

/// <summary>Turns a discovered light, room, or zone on.</summary>
public sealed record TurnOnIntent(string TargetName, HueResourceType? TargetType, string? TargetGroupName = null,
    HueResourceType? TargetGroupType = null) : HueIntent(TargetName, TargetType, TargetGroupName, TargetGroupType);

/// <summary>Turns a discovered light, room, or zone off.</summary>
public sealed record TurnOffIntent(string TargetName, HueResourceType? TargetType, string? TargetGroupName = null,
    HueResourceType? TargetGroupType = null) : HueIntent(TargetName, TargetType, TargetGroupName, TargetGroupType);

/// <summary>Sets the brightness of a discovered light, room, or zone.</summary>
public sealed record SetBrightnessIntent(string TargetName, HueResourceType? TargetType, int? BrightnessPercent,
    string? TargetGroupName = null, HueResourceType? TargetGroupType = null)
    : HueIntent(TargetName, TargetType, TargetGroupName, TargetGroupType);

/// <summary>Sets a named color on a discovered light, room, or zone.</summary>
public sealed record SetColorIntent(string TargetName, HueResourceType? TargetType, string? Color,
    string? TargetGroupName = null, HueResourceType? TargetGroupType = null)
    : HueIntent(TargetName, TargetType, TargetGroupName, TargetGroupType);

/// <summary>Activates a discovered Hue scene.</summary>
public sealed record ActivateSceneIntent(string TargetName, HueResourceType? TargetType, string? TargetGroupName = null,
    HueResourceType? TargetGroupType = null) : HueIntent(TargetName, TargetType, TargetGroupName, TargetGroupType);

/// <summary>Requests the current state of a discovered light, room, or zone.</summary>
public sealed record GetStatusIntent(string TargetName, HueResourceType? TargetType, string? TargetGroupName = null,
    HueResourceType? TargetGroupType = null) : HueIntent(TargetName, TargetType, TargetGroupName, TargetGroupType);

/// <summary>Requests matching resources from the discovered Hue catalog.</summary>
public sealed record ListResourcesIntent(string? TargetName, HueResourceType? TargetType, string? TargetGroupName = null,
    HueResourceType? TargetGroupType = null) : HueIntent(TargetName, TargetType, TargetGroupName, TargetGroupType);

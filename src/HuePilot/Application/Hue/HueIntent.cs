namespace HuePilot.Application.Hue;

/// <summary>
/// A model-produced interpretation of a user's request. It is untrusted until a discovered resource is resolved
/// and its operation and arguments are validated by the application.
/// </summary>
/// <param name="Operation">The requested operation, or <see langword="null" /> when it is unclear.</param>
/// <param name="TargetName">The discovered room, zone, light, or scene name.</param>
/// <param name="TargetType">
/// The model-supplied resource kind: room, zone, light, or scene. This remains a string because it is untrusted
/// provider input; resolved application resources use <see cref="HueResourceTypes" /> constants instead.
/// </param>
/// <param name="BrightnessPercent">Brightness from 1 through 100 for a brightness operation.</param>
/// <param name="Color">A supported named color for a color operation.</param>
/// <param name="TargetGroupName">Optional room or zone used to disambiguate an associated light or scene.</param>
/// <param name="TargetGroupType">
/// Optional model-supplied kind of the requested group, such as room or zone. It is validated against discovered
/// resources before use.
/// </param>
/// <param name="ClarificationQuestion">A follow-up question when the model cannot safely infer one action.</param>
public sealed record HueIntent(
    HueOperation? Operation,
    string? TargetName,
    string? TargetType,
    int? BrightnessPercent = null,
    string? Color = null,
    string? TargetGroupName = null,
    string? TargetGroupType = null,
    string? ClarificationQuestion = null)
{
    /// <summary>Gets whether the model asked the user to clarify the request.</summary>
    public bool NeedsClarification => !string.IsNullOrWhiteSpace(ClarificationQuestion);
}

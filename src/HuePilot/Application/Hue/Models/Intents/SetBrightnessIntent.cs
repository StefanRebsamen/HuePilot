namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Sets the brightness of a resource.</summary>
public sealed record SetBrightnessIntent(Guid TargetResourceId, int? BrightnessPercent, Guid? TargetGroupId = null)
    : TargetedHueIntent(TargetResourceId, TargetGroupId);
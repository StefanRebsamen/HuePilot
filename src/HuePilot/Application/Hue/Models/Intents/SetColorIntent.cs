namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Sets a named color on a resource.</summary>
public sealed record SetColorIntent(Guid TargetResourceId, string? Color, Guid? TargetGroupId = null)
    : TargetedHueIntent(TargetResourceId, TargetGroupId);
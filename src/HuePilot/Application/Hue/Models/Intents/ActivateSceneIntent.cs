namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Activates an existing scene.</summary>
public sealed record ActivateSceneIntent(Guid TargetResourceId, Guid? TargetGroupId = null)
    : TargetedHueIntent(TargetResourceId, TargetGroupId);
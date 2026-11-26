namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Reads a resource's status.</summary>
public sealed record GetStatusIntent(Guid TargetResourceId, Guid? TargetGroupId = null)
    : TargetedHueIntent(TargetResourceId, TargetGroupId);
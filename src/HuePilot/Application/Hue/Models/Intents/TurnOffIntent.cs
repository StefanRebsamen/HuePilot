namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Turns a resource off.</summary>
public sealed record TurnOffIntent(Guid TargetResourceId, Guid? TargetGroupId = null)
    : TargetedHueIntent(TargetResourceId, TargetGroupId);
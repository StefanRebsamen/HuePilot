namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Turns a resource on.</summary>
public sealed record TurnOnIntent(Guid TargetResourceId, Guid? TargetGroupId = null)
    : TargetedHueIntent(TargetResourceId, TargetGroupId);
namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>An operation on one discovered resource, optionally scoped to a room or zone.</summary>
public abstract record TargetedHueIntent(Guid TargetResourceId, Guid? TargetGroupId) : HueIntent;
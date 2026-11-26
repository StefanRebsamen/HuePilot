namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Lists resources of a category, optionally within one discovered group.</summary>
public sealed record ListResourcesIntent(HueResourceType ResourceType, Guid? TargetGroupId = null) : HueIntent;
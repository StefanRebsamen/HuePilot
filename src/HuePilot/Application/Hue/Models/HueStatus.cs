namespace HuePilot.Application.Hue.Models;

/// <summary>Current readable state of a discovered Hue light, room, or zone.</summary>
public sealed record HueStatus(
    string ResourceId,
    string ResourceName,
    HueResourceType ResourceType,
    bool IsOn,
    int? BrightnessPercent = null,
    double? ColorX = null,
    double? ColorY = null);

namespace HuePilot.Application.Hue;

/// <summary>Current readable state of a discovered Hue light, room, or zone.</summary>
public sealed record HueStatus(
    string ResourceId,
    string ResourceName,
    string ResourceType,
    bool IsOn,
    int? BrightnessPercent = null,
    double? ColorX = null,
    double? ColorY = null);

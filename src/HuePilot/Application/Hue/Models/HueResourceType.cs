namespace HuePilot.Application.Hue.Models;

/// <summary>Hue resource kinds that can be named by an intent.</summary>
public enum HueResourceType
{
    /// <summary>A Hue room.</summary>
    Room,

    /// <summary>A Hue zone.</summary>
    Zone,

    /// <summary>A Hue light.</summary>
    Light,

    /// <summary>A Hue scene.</summary>
    Scene,

    /// <summary>A Hue device.</summary>
    Device,

    /// <summary>A Hue grouped-light service.</summary>
    GroupedLight
}
namespace HuePilot.Application.Hue.Models;

/// <summary>Converts intent resource kinds to and from Bridge resource identifiers.</summary>
public static class HueResourceTypeExtensions
{
    /// <summary>Gets the Hue v2 wire resource-type identifier for an application resource kind.</summary>
    public static string ToHueV2WireType(this HueResourceType type) => type switch
    {
        HueResourceType.Room => "room", HueResourceType.Zone => "zone", HueResourceType.Light => "light",
        HueResourceType.Scene => "scene", HueResourceType.Device => "device", HueResourceType.GroupedLight => "grouped_light",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported Hue resource type.")
    };

    /// <summary>Converts a Hue v2 wire resource-type identifier to an application resource kind.</summary>
    public static bool TryParseHueV2WireType(string? identifier, out HueResourceType type)
    {
        switch (identifier?.Trim().ToLowerInvariant())
        {
            case "room":
                type = HueResourceType.Room;
                return true;
            case "zone":
                type = HueResourceType.Zone;
                return true;
            case "light":
                type = HueResourceType.Light;
                return true;
            case "scene":
                type = HueResourceType.Scene;
                return true;
            case "device":
                type = HueResourceType.Device;
                return true;
            case "grouped_light":
                type = HueResourceType.GroupedLight;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
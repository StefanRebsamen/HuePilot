using HuePilot.Application.Hue.Models;

namespace HuePilot.Application.Hue;

/// <summary>Deterministic checks that constrain a model-proposed intent to supported Hue actions.</summary>
public static class HueActionValidator
{
    private static readonly string[] AllowedColors =
    [
        "red", "orange", "yellow", "green", "cyan", "blue", "purple", "pink", "white", "warm white", "cool white"
    ];

    /// <summary>Creates an executable action only when the intent matches the resolved resource and valid ranges.</summary>
    public static bool TryCreate(HueIntent intent, HueResource target, out HueAction? action, out string error)
    {
        if (intent is SetBrightnessIntent { BrightnessPercent: not (>= 1 and <= 100) })
        {
            action = null;
            error = "Brightness must be a whole-number percentage from 1 to 100.";
            return false;
        }

        if (intent is SetColorIntent colorIntent)
        {
            if (string.IsNullOrWhiteSpace(colorIntent.Color))
            {
                action = null;
                error = "I couldn't determine a color. Which color should I use?";
                return false;
            }

            if (!AllowedColors.Contains(colorIntent.Color.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                action = null;
                error = "Please choose a named color such as red, orange, yellow, green, blue, purple, pink, or white.";
                return false;
            }

            if (string.Equals(colorIntent.Color, "pink", StringComparison.OrdinalIgnoreCase))
            {
                action = null;
                error = "I can't stand pink!!!!!";
                return false;
            }
        }

        if (intent is ListResourcesIntent)
        {
            action = new HueAction(HueOperation.ListResources, target);
            error = string.Empty;
            return true;
        }

        if (target.Type is not (HueResourceType.Room or HueResourceType.Zone or HueResourceType.Light or HueResourceType.Scene))
        {
            action = null;
            error = "That Hue resource type isn't supported.";
            return false;
        }

        if (intent is ActivateSceneIntent && !IsScene(target))
        {
            action = null;
            error = "Only an existing scene can be activated as a scene.";
            return false;
        }

        if (intent is not ActivateSceneIntent && IsScene(target))
        {
            action = null;
            error = "That operation can't be applied to a scene.";
            return false;
        }

        action = intent switch
        {
            TurnOnIntent => new HueAction(HueOperation.TurnOn, target),
            TurnOffIntent => new HueAction(HueOperation.TurnOff, target),
            SetBrightnessIntent brightness => new HueAction(HueOperation.SetBrightness, target, brightness.BrightnessPercent),
            SetColorIntent color => new HueAction(HueOperation.SetColor, target, Color: color.Color),
            ActivateSceneIntent => new HueAction(HueOperation.ActivateScene, target),
            GetStatusIntent => new HueAction(HueOperation.GetStatus, target),
            _ => null
        };
        if (action is not null)
        {
            error = string.Empty;
            return true;
        }

        error = "That action isn't supported.";
        return false;
    }

    private static bool IsScene(HueResource target) =>
        target.Type == HueResourceType.Scene;
}

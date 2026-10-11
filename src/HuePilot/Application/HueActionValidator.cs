namespace HuePilot.Application;

/// <summary>Deterministic checks that constrain a model-proposed intent to supported Hue actions.</summary>
public static class HueActionValidator
{
    /// <summary>Creates an executable action only when the intent matches the resolved resource and valid ranges.</summary>
    /// <param name="intent">The untrusted structured intent returned by an AI parser.</param>
    /// <param name="target">The exact Hue resource resolved by application code.</param>
    /// <param name="action">The validated action, or <see langword="null"/> when rejected.</param>
    /// <param name="error">A user-readable rejection reason, or an empty string on success.</param>
    /// <returns><see langword="true"/> when a safe action was created.</returns>
    public static bool TryCreate(HueIntent intent, HueResource target, out HueAction? action, out string error)
    {
        action = null;
        error = "That action isn't supported.";

        if (intent.Operation is null)
            return false;

        if (intent.Operation == HueOperation.SetBrightness && intent.BrightnessPercent is not (>= 1 and <= 100))
        {
            error = "Brightness must be a whole-number percentage from 1 to 100.";
            return false;
        }

        if (intent.Operation == HueOperation.SetColor && string.IsNullOrWhiteSpace(intent.Color))
        {
            error = "I couldn't determine a color. Which color should I use?";
            return false;
        }

        // Keep the color vocabulary closed so arbitrary model text never reaches the Hue adapter.
        if (intent.Operation == HueOperation.SetColor && !AllowedColors.Contains(intent.Color!.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            error = "Please choose a named color such as red, orange, yellow, green, blue, purple, pink, or white.";
            return false;
        }

        if (target.Type is not ("room" or "light" or "scene"))
        {
            error = "That Hue resource type isn't supported.";
            return false;
        }

        if (intent.Operation == HueOperation.ActivateScene && !target.Type.Equals("scene", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only an existing scene can be activated as a scene.";
            return false;
        }

        if (intent.Operation != HueOperation.ActivateScene && target.Type.Equals("scene", StringComparison.OrdinalIgnoreCase))
        {
            error = "That operation can't be applied to a scene.";
            return false;
        }

        action = new(intent.Operation.Value, target, intent.BrightnessPercent, intent.Color);
        error = string.Empty;
        return true;
    }

    private static readonly string[] AllowedColors = ["red", "orange", "yellow", "green", "cyan", "blue", "purple", "pink", "white", "warm white", "cool white"];
}

namespace HuePilot.Application;

/// <summary>A validated operation and its resolved Hue target.</summary>
/// <param name="Operation">The operation to execute.</param>
/// <param name="Target">The exact discovered resource to control.</param>
/// <param name="BrightnessPercent">Optional brightness value for <see cref="HueOperation.SetBrightness"/>.</param>
/// <param name="Color">Optional named color for <see cref="HueOperation.SetColor"/>.</param>
public sealed record HueAction(HueOperation Operation, HueResource Target, int? BrightnessPercent = null, string? Color = null);

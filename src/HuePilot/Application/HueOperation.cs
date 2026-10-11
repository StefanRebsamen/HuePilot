namespace HuePilot.Application;

/// <summary>Operations that HuePilot is allowed to perform on discovered Hue resources.</summary>
public enum HueOperation
{
    /// <summary>Turns on a light or room.</summary>
    TurnOn,
    /// <summary>Turns off a light or room.</summary>
    TurnOff,
    /// <summary>Sets brightness to a whole-number percentage.</summary>
    SetBrightness,
    /// <summary>Sets a supported named color.</summary>
    SetColor,
    /// <summary>Activates an existing Hue scene.</summary>
    ActivateScene
}

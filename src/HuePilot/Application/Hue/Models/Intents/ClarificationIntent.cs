namespace HuePilot.Application.Hue.Models.Intents;

/// <summary>Requests user input before an action can be proposed.</summary>
public sealed record ClarificationIntent(string Question) : HueIntent;
namespace HuePilot.Application.Chat;

/// <summary>Provides human-readable names for AI intent-planning approaches.</summary>
public static class AiApproachExtensions
{
    /// <summary>Gets the approach name displayed in chat technical details.</summary>
    public static string GetDisplayName(this AiApproach approach)
    {
        return approach switch
        {
            AiApproach.Structured => "Structured output",
            AiApproach.Tools => "Tool calling",
            _ => throw new ArgumentOutOfRangeException(nameof(approach), approach, null)
        };
    }
}
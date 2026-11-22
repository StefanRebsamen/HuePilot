namespace HuePilot.Application;

/// <summary>Identifies an AI intent-planning approach.</summary>
public enum AiApproach
{
    /// <summary>Structured JSON output.</summary>
    Structured,

    /// <summary>Bounded tool calling.</summary>
    Tools
}

/// <summary>Provides human-readable names for AI intent-planning approaches.</summary>
public static class AiApproachExtensions
{
    /// <summary>Gets the approach name displayed in chat technical details.</summary>
    public static string GetDisplayName(this AiApproach approach) => approach switch
    {
        AiApproach.Structured => "Structured output",
        AiApproach.Tools => "Tool calling",
        _ => throw new ArgumentOutOfRangeException(nameof(approach), approach, null)
    };
}

/// <summary>Builds privacy-safe technical details for a chat response.</summary>
public static class AiTechnicalDetails
{
    /// <summary>Includes the selected approach and, when enabled, non-empty diagnostics.</summary>
    public static string Format(AiApproach approach, string? diagnostics, bool includeDiagnostics)
    {
        var details = $"AI approach: {approach.GetDisplayName()}";
        return !includeDiagnostics || string.IsNullOrWhiteSpace(diagnostics)
            ? details
            : $"{details}{Environment.NewLine}{Environment.NewLine}{diagnostics}";
    }
}

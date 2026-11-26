namespace HuePilot.Application.Chat;

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
namespace HuePilot.Application;

/// <summary>Identifies an AI intent-planning approach.</summary>
public enum AiApproach
{
    /// <summary>Structured JSON output.</summary>
    Structured,

    /// <summary>Bounded tool calling.</summary>
    Tools
}
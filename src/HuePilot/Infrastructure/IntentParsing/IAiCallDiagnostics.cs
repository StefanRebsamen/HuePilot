namespace HuePilot.Infrastructure.IntentParsing;

/// <summary>
/// Exposes diagnostics for the most recent parse operation to the optional AI evaluation runner.
/// Implementations keep these values in memory only; they are not an audit or persistence mechanism.
/// </summary>
public interface IAiCallDiagnostics
{
    /// <summary>The number of provider requests made by the last parse operation.</summary>
    int LastModelCallCount { get; }

    /// <summary>The number of tool invocations made during the last parse operation.</summary>
    int LastToolCallCount { get; }

    /// <summary>
    /// The application-to-model exchange from the last parse operation, intended for local evaluation only.
    /// It can contain user-provided text and must be handled as sensitive diagnostic data.
    /// </summary>
    string LastConversationLog { get; }
}

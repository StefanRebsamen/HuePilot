namespace HuePilot.Infrastructure;

/// <summary>Exposes per-request model/tool call counts for the optional AI evaluation runner.</summary>
public interface IAiCallDiagnostics
{
    /// <summary>The number of provider requests made by the last parse operation.</summary>
    int LastModelCallCount { get; }

    /// <summary>The number of tool invocations made during the last parse operation.</summary>
    int LastToolCallCount { get; }
}

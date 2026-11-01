using HuePilot.Application;

namespace HuePilot.Infrastructure;

/// <summary>Stores the selected AI approach for the current browser circuit.</summary>
public sealed class AiApproachState
{
    /// <summary>Creates circuit state from application defaults.</summary>
    public AiApproachState(IConfiguration configuration)
    {
        Current = string.Equals(configuration["AI:Approach"], "tools", StringComparison.OrdinalIgnoreCase)
            ? AiApproach.Tools
            : AiApproach.Structured;
    }

    /// <summary>Gets the currently selected approach.</summary>
    public AiApproach Current { get; private set; }

    /// <summary>Changes the approach used by subsequent requests.</summary>
    public void Set(AiApproach approach) => Current = approach;
}

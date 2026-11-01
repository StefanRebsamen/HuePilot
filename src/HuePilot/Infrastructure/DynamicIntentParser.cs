using HuePilot.Application;
using HuePilot.Infrastructure.Structured;
using HuePilot.Infrastructure.ToolCalling;

namespace HuePilot.Infrastructure;

/// <summary>Selects an intent parser for each request based on circuit state.</summary>
public sealed class DynamicIntentParser : IIntentParser, IAiCallDiagnostics
{
    private readonly AiApproachState state;
    private readonly IConfiguration configuration;
    private IIntentParser? active;

    /// <summary>Creates the dynamic parser.</summary>
    public DynamicIntentParser(AiApproachState state, IConfiguration configuration)
    {
        this.state = state;
        this.configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<HueIntent> ParseAsync(string request, IReadOnlyList<ChatTurn> history, IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
    {
        active = state.Current == AiApproach.Tools
            ? new OpenAiToolCallingIntentParser(configuration)
            : new OpenAiStructuredIntentParser(configuration);
        return await active.ParseAsync(request, history, availableResources, cancellationToken);
    }

    /// <inheritdoc />
    public int LastModelCallCount => (active as IAiCallDiagnostics)?.LastModelCallCount ?? 0;
    /// <inheritdoc />
    public int LastToolCallCount => (active as IAiCallDiagnostics)?.LastToolCallCount ?? 0;
    /// <inheritdoc />
    public string LastConversationLog => (active as IAiCallDiagnostics)?.LastConversationLog ?? "";
}

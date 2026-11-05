using HuePilot.Application;
using HuePilot.Application.Hue;

namespace HuePilot.Infrastructure.IntentParsing;

/// <summary>Selects an intent parser for each request based on circuit state.</summary>
public sealed class DynamicIntentParser : IIntentParser, IAiCallDiagnostics
{
    private readonly IConfiguration _configuration;
    private readonly AiApproachState _state;
    private IIntentParser? _active;

    /// <summary>Creates the dynamic parser.</summary>
    public DynamicIntentParser(AiApproachState state, IConfiguration configuration)
    {
        _state = state;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public int LastModelCallCount => (_active as IAiCallDiagnostics)?.LastModelCallCount ?? 0;

    /// <inheritdoc />
    public int LastToolCallCount => (_active as IAiCallDiagnostics)?.LastToolCallCount ?? 0;

    /// <inheritdoc />
    public string LastConversationLog => (_active as IAiCallDiagnostics)?.LastConversationLog ?? "";

    /// <inheritdoc />
    public async Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
    {
        _active = _state.Current == AiApproach.Tools
            ? new OpenAiToolCallingIntentParser(_configuration)
            : new OpenAiStructuredIntentParser(_configuration);
        return await _active.ParseAsync(request, history, availableResources, cancellationToken);
    }
}
using HuePilot.Application;
using HuePilot.Application.Hue;

namespace HuePilot.Infrastructure.IntentParsing;

/// <summary>
/// Selects the parsing strategy configured for the current circuit and exposes that strategy's diagnostics.
/// A parser is created for each request so counters and conversation logs cannot leak between requests.
/// </summary>
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

    /// <summary>Parses a request with the parser selected for the current circuit.</summary>
    public async Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
    {
        // The selected approach controls how the model is constrained; both implementations still return
        // untrusted intents that the application layer must validate before executing Hue operations.
        _active = _state.Current switch
        {
            AiApproach.Tools => new OpenAiToolCallingIntentParser(_configuration),
            AiApproach.Structured => new OpenAiStructuredIntentParser(_configuration),
            _ => throw new NotSupportedException()
        };

        return await _active.ParseAsync(request, history, availableResources, cancellationToken);
    }
}

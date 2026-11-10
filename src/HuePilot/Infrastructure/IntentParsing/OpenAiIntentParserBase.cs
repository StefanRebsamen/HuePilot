using HuePilot.Application;
using HuePilot.Application.Hue;
using OpenAI.Chat;

namespace HuePilot.Infrastructure.IntentParsing;

/// <summary>
/// Provides the common OpenAI client setup and conversation-history conversion used by Hue intent parsers.
/// </summary>
public abstract class OpenAiIntentParserBase : IIntentParser
{
    private const int MaxHistoryTurns = 12;
    private readonly IConfiguration _configuration;

    /// <summary>Initializes the parser with OpenAI configuration.</summary>
    protected OpenAiIntentParserBase(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default);

    /// <summary>Creates the configured OpenAI chat client.</summary>
    protected ChatClient CreateClient()
    {
        var apiKey = _configuration["OpenAI:ApiKey"] ?? _configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI is not configured. Set the OpenAI:ApiKey user secret to enable AI requests.");
        }

        return new ChatClient(_configuration["OpenAI:Model"] ?? _configuration["OPENAI_MODEL"] ?? "gpt-4.1-mini",
            apiKey);
    }

    /// <summary>Builds the common system and recent conversation messages.</summary>
    protected static List<ChatMessage> CreateConversationMessages(string systemPrompt, IReadOnlyList<ChatTurn> history)
    {
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };
        foreach (var turn in history.TakeLast(MaxHistoryTurns))
        {
            switch (turn.Role)
            {
                case "user":
                    messages.Add(new UserChatMessage(turn.Content));
                    break;
                case "assistant":
                    messages.Add(new AssistantChatMessage(turn.Content));
                    break;
            }
        }

        return messages;
    }
}

using HuePilot.Application;
using HuePilot.Application.Hue;
using HuePilot.Infrastructure.IntentParsing;
using OpenAI.Chat;

namespace HuePilot.Tests;

public sealed class OpenAiIntentParserBaseTests
{
    [Fact]
    public void CreateMessages_IncludesSystemMessageAndTwelveMostRecentSupportedHistoryTurns()
    {
        var history = Enumerable.Range(1, 14)
            .Select(index => new ChatTurn(index % 2 == 0 ? "assistant" : "user", $"message {index}"))
            .ToArray();

        var messages = TestIntentParser.CreateMessages("system", history);

        Assert.Equal(13, messages.Count);
        Assert.IsType<SystemChatMessage>(messages[0]);
        Assert.DoesNotContain(messages, message => message.Content.Any(content => content.Text == "message 1"));
        Assert.DoesNotContain(messages, message => message.Content.Any(content => content.Text == "message 2"));
        Assert.Contains(messages, message => message is UserChatMessage && message.Content.Any(content => content.Text == "message 13"));
        Assert.Contains(messages,
            message => message is AssistantChatMessage && message.Content.Any(content => content.Text == "message 14"));
    }

    private sealed class TestIntentParser : OpenAiIntentParserBase
    {
        private TestIntentParser() : base(null!)
        {
        }

        public static List<ChatMessage> CreateMessages(string systemPrompt, IReadOnlyList<ChatTurn> history) =>
            CreateConversationMessages(systemPrompt, history);

        public override Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
            IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

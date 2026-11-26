using System.Reflection;
using System.Text.Json;
using HuePilot.Application.Chat;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;
using HuePilot.Application.IntentParsing;
using OpenAI.Chat;

namespace HuePilot.Tests;

public sealed class OpenAiIntentParserBaseTests
{
    [Fact]
    public void StructuredCatalog_ListsGroupsOnlyForExactMemberIds()
    {
        var officeDesk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var kitchenDesk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        HueResource[] resources =
        [
            officeDesk, kitchenDesk,
            new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, [officeDesk])
        ];

        var createMessages =
            typeof(OpenAiStructuredIntentParser).GetMethod("CreateMessages",
                BindingFlags.Static | BindingFlags.NonPublic)!;
        var messages =
            Assert.IsType<List<ChatMessage>>(createMessages.Invoke(null,
                ["Desk lamp on", Array.Empty<ChatTurn>(), resources]));
        var message = Assert.IsType<UserChatMessage>(messages[^1]);
        var catalog = message.Content.Single().Text.Split('\n')[0]["Available Hue resources: ".Length..];
        using var document = JsonDocument.Parse(catalog);
        var matchingLights = document.RootElement.EnumerateArray()
            .Where(resource => resource.GetProperty("Name").GetString() == "Desk lamp").ToArray();

        Assert.Equal(2, matchingLights.Length);
        var group = Assert.Single(matchingLights[0].GetProperty("groups").EnumerateArray());
        Assert.Equal(resources[2].Id, group.GetProperty("Id").GetGuid());
        Assert.Equal("Office", group.GetProperty("Name").GetString());
        Assert.Equal((int)HueResourceType.Room, group.GetProperty("Type").GetInt32());
        Assert.Empty(matchingLights[1].GetProperty("groups").EnumerateArray());
    }

    [Fact]
    public void CreateMessages_IncludesSystemMessageAndTwelveMostRecentSupportedHistoryTurns()
    {
        var history = Enumerable.Range(1, 14)
            .Select(index => new ChatTurn(index % 2 == 0 ? "assistant" : "user", $"message {index}")).ToArray();

        var messages = TestIntentParser.CreateMessages("system", history);

        Assert.Equal(13, messages.Count);
        Assert.IsType<SystemChatMessage>(messages[0]);
        Assert.DoesNotContain(messages, message => message.Content.Any(content => content.Text == "message 1"));
        Assert.DoesNotContain(messages, message => message.Content.Any(content => content.Text == "message 2"));
        Assert.Contains(messages,
            message => message is UserChatMessage && message.Content.Any(content => content.Text == "message 13"));
        Assert.Contains(messages,
            message => message is AssistantChatMessage && message.Content.Any(content => content.Text == "message 14"));
    }

    private sealed class TestIntentParser : OpenAiIntentParserBase
    {
        private TestIntentParser() : base(null!)
        {
        }

        public static List<ChatMessage> CreateMessages(string systemPrompt, IReadOnlyList<ChatTurn> history)
        {
            return CreateConversationMessages(systemPrompt, history);
        }

        public override Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
            IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
using HuePilot.Application;
using HuePilot.Infrastructure;
using HuePilot.Infrastructure.ToolCalling;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HuePilot.Tests;

public sealed class HueConversationServiceTests
{
    private static readonly HueResource Office = new("room-1", "Office", "room");

    [Fact]
    public async Task ClearRequestExecutesValidatedAction()
    {
        var hue = new FakeHueService([Office]);
        var service = new HueConversationService(new FakeIntentParser(new(HueOperation.SetBrightness, "Office", "room", 65)), hue);

        var result = await service.PlanAsync("Set the office to 65 percent", []);

        Assert.Equal(HueOperation.SetBrightness, result.ProposedAction?.Operation);
        Assert.Equal(65, result.ProposedAction?.BrightnessPercent);
        Assert.Empty(hue.Executed);
        Assert.Contains("65%", result.AssistantMessage);
        await service.ExecuteAsync(result.ProposedAction!);
        Assert.Single(hue.Executed);
    }

    [Fact]
    public async Task AmbiguousRequestAsksAndDoesNotExecute()
    {
        var hue = new FakeHueService([Office]);
        var service = new HueConversationService(new FakeIntentParser(new(null, null, null, ClarificationQuestion: "Which room?")), hue);

        var result = await service.PlanAsync("Turn on the lights", []);

        Assert.True(result.NeedsClarification);
        Assert.Equal("Which room?", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void BrightnessOutsideRangeIsRejected(int brightness)
    {
        var intent = new HueIntent(HueOperation.SetBrightness, "Office", "room", brightness);

        Assert.False(HueActionValidator.TryCreate(intent, Office, out _, out var error));
        Assert.Contains("1 to 100", error);
    }

    [Fact]
    public async Task ListResourcesReportsLightsAssociatedWithRoom()
    {
        var office = new HueResourceGroup("room-1", "Office", "room");
        var desk = new HueResource("light-1", "Desk lamp", "light", [office]);
        var ceiling = new HueResource("light-2", "Ceiling light", "light", [office]);
        var hue = new FakeHueService([Office, desk, ceiling]);
        var service = new HueConversationService(new FakeIntentParser(new(HueOperation.ListResources, "Office", "light")), hue);

        var result = await service.PlanAsync("What lamps are in the office?", []);

        Assert.Contains("Desk lamp", result.AssistantMessage);
        Assert.Contains("Ceiling light", result.AssistantMessage);
        Assert.Null(result.ProposedAction);
    }

    [Fact]
    public async Task ZoneCanBeControlled()
    {
        var zone = new HueResource("zone-upstairs", "Upstairs", "zone");
        var hue = new FakeHueService([zone]);
        var service = new HueConversationService(
            new FakeIntentParser(new(HueOperation.TurnOn, "Upstairs", "zone")), hue);

        var result = await service.PlanAsync("Turn on upstairs", []);

        Assert.Equal("zone-upstairs", result.ProposedAction?.Target.Id);
        Assert.Equal(HueOperation.TurnOn, result.ProposedAction?.Operation);
    }

    [Fact]
    public async Task ParserCanReturnMultipleIntents()
    {
        var desk = new HueResource("light-desk", "Desk", "light");
        var ceiling = new HueResource("light-ceiling", "Ceiling", "light");
        var hue = new FakeHueService([desk, ceiling]);
        var parser = new FakeIntentParser(
            new(HueOperation.TurnOn, "Desk", "light"),
            new(HueOperation.TurnOn, "Ceiling", "light")
        );
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk and Ceiling", []);

        Assert.Equal(2, result.ProposedActions.Count);
        Assert.Equal(["light-desk", "light-ceiling"], result.ProposedActions.Select(action => action.Target.Id));
    }

    [Fact]
    public async Task MultipleGroupedLightsKeepTheirOwnNames()
    {
        var office = new HueResourceGroup("room-office", "Office", "room");
        var desk = new HueResource("light-desk", "Desk", "light", [office]);
        var pinboard = new HueResource("light-pinboard", "Pinboard", "light", [office]);
        var hue = new FakeHueService([desk, pinboard]);
        var parser = new FakeIntentParser(
            new(HueOperation.TurnOn, "Desk", "light", TargetGroupName: "Office", TargetGroupType: "room"),
            new(HueOperation.TurnOn, "Pinboard", "light", TargetGroupName: "Office", TargetGroupType: "room"));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk and Pinboard", []);

        Assert.Equal(["light-desk", "light-pinboard"], result.ProposedActions.Select(action => action.Target.Id));
    }

    [Fact]
    public void ToolLookupResolvesRoomNameToMemberLights()
    {
        var office = new HueResourceGroup("room-office", "Office", "room");
        var resources = new HueResource[]
        {
            new("room-office", "Office", "room"),
            new("light-desk", "Desk lamp", "light", [office]),
            new("light-ceiling", "Ceiling light", "light", [office])
        };

        var json = OpenAiToolCallingIntentParser.SearchResources(
            """{"query":"Office","resourceType":"light","groupName":"","groupId":""}""", resources);
        using var document = JsonDocument.Parse(json);
        var matches = document.RootElement.GetProperty("resources").EnumerateArray().ToArray();

        Assert.Equal(2, matches.Length);
        Assert.Contains(matches, match => match.GetProperty("name").GetString() == "Desk lamp");
        Assert.Contains(matches, match => match.GetProperty("name").GetString() == "Ceiling light");
        Assert.Equal("room-office", document.RootElement.GetProperty("matchedGroups").EnumerateArray()
            .Single().GetProperty("id").GetString());
    }

    [Fact]
    public async Task UnknownTargetDoesNotExecute()
    {
        var hue = new FakeHueService([Office]);
        var service = new HueConversationService(new FakeIntentParser(new(HueOperation.TurnOff, "Kitchen", "room")), hue);

        var result = await service.PlanAsync("Turn off kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task DuplicateNamesAskForClarification()
    {
        var hue = new FakeHueService([Office, new("room-2", "Office", "room")]);
        var service = new HueConversationService(new FakeIntentParser(new(HueOperation.TurnOn, "Office", "room")), hue);

        var result = await service.PlanAsync("Turn on the office", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("more than one", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task DuplicateSceneNamesResolveUsingRoomAssociation()
    {
        var bedroom = new HueResourceGroup("room-bedroom", "Bedroom", "room");
        var kitchen = new HueResourceGroup("room-kitchen", "Kitchen", "room");
        var scenes = new HueResource[]
        {
            new("scene-bedroom", "Relax", "scene", [bedroom]),
            new("scene-kitchen", "Relax", "scene", [kitchen])
        };
        var hue = new FakeHueService(scenes);
        var parser = new FakeIntentParser(new(HueOperation.ActivateScene, "Relax", "scene", TargetGroupName: "Bedroom", TargetGroupType: "room"));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Activate Relax in the Bedroom", []);

        Assert.Equal("scene-bedroom", result.ProposedAction?.Target.Id);
        Assert.Contains("Bedroom", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task DuplicateSceneNamesClarifyWithAvailableRoomAssociations()
    {
        var hue = new FakeHueService([
            new("scene-bedroom", "Relax", "scene", [new("room-bedroom", "Bedroom", "room")]),
            new("scene-kitchen", "Relax", "scene", [new("room-kitchen", "Kitchen", "room")])
        ]);
        var service = new HueConversationService(new FakeIntentParser(new(HueOperation.ActivateScene, "Relax", "scene")), hue);

        var result = await service.PlanAsync("Activate Relax", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("Bedroom", result.AssistantMessage);
        Assert.Contains("Kitchen", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task SceneCannotBeResolvedToUnassociatedRoom()
    {
        var hue = new FakeHueService([new("scene-bedroom", "Relax", "scene", [new("room-bedroom", "Bedroom", "room")])]);
        var service = new HueConversationService(new FakeIntentParser(new(HueOperation.ActivateScene, "Relax", "scene", TargetGroupName: "Kitchen", TargetGroupType: "room")), hue);

        var result = await service.PlanAsync("Activate Relax in the Kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("associated with room ‘Kitchen’", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task RequestNamingMultipleGroupsAsksBeforeActing()
    {
        var hue = new FakeHueService([
            new("scene-bedroom", "Relax", "scene", [new("room-bedroom", "Bedroom", "room")]),
            new("scene-kitchen", "Relax", "scene", [new("room-kitchen", "Kitchen", "room")])
        ]);
        var service = new HueConversationService(new FakeIntentParser(new(HueOperation.ActivateScene, "Relax", "scene")), hue);

        var result = await service.PlanAsync("Activate Relax in Bedroom and Kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("multiple rooms or zones", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public void UnsupportedColorIsRejected()
    {
        var intent = new HueIntent(HueOperation.SetColor, "Office", "room", Color: "ultraviolet");

        Assert.False(HueActionValidator.TryCreate(intent, Office, out _, out var error));
        Assert.Contains("named color", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public async Task PairingRejectsInvalidIpBeforeNetworkCall(string ipAddress)
    {
        var setup = new HueBridgeSetupService();

        await Assert.ThrowsAsync<ArgumentException>(() => setup.PairAsync(ipAddress));
    }

    [Fact]
    public async Task HueAdapterFailsClearlyWhenBridgeSecretsAreMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hue = new HueApiHueService(configuration);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => hue.GetResourcesAsync());

        Assert.Contains("/setup", error.Message);
    }

    private sealed class FakeIntentParser : IIntentParser
    {
        private readonly IReadOnlyList<HueIntent> _intents;

        public FakeIntentParser(HueIntent intent) => _intents = [intent];

        public FakeIntentParser(HueIntent first, HueIntent second, params HueIntent[] additional)
            => _intents = [first, second, .. additional];

        public Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
            IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default) =>
            Task.FromResult(_intents);
    }

    private sealed class FakeHueService(IReadOnlyList<HueResource> resources) : IHueService
    {
        public List<HueAction> Executed { get; } = [];
        public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(resources);
        public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
        {
            Executed.Add(action);
            return Task.CompletedTask;
        }
    }
}

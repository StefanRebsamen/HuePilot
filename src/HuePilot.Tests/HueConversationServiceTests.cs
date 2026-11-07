using HuePilot.Application;
using HuePilot.Application.Hue;
using HuePilot.Infrastructure.IntentParsing;

namespace HuePilot.Tests;

public sealed class HueConversationServiceTests
{
    private static readonly HueResource Office = new("room-1", "Office", "room");

    [Fact]
    public async Task PlanAsync_RequestIsClear_ReturnsValidatedActionWithoutExecutingIt()
    {
        var hue = new FakeHueService([Office]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new HueIntent(HueOperation.SetBrightness, "Office", "room", 65)), hue);

        var result = await service.PlanAsync("Set the office to 65 percent", []);

        Assert.Equal(HueOperation.SetBrightness, result.ProposedAction?.Operation);
        Assert.Equal(65, result.ProposedAction?.BrightnessPercent);
        Assert.Empty(hue.Executed);
        Assert.Contains("65%", result.AssistantMessage);
        await service.ExecuteAsync(result.ProposedAction!);
        Assert.Single(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestIsAmbiguous_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([Office]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new HueIntent(null, null, null, ClarificationQuestion: "Which room?")), hue);

        var result = await service.PlanAsync("Turn on the lights", []);

        Assert.True(result.NeedsClarification);
        Assert.Equal("Which room?", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestListsLightsInRoom_ReturnsAssociatedLights()
    {
        var office = new HueResourceGroup("room-1", "Office", "room");
        var desk = new HueResource("light-1", "Desk lamp", "light", [office]);
        var ceiling = new HueResource("light-2", "Ceiling light", "light", [office]);
        var hue = new FakeHueService([Office, desk, ceiling]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new HueIntent(HueOperation.ListResources, "Office", "light")), hue);

        var result = await service.PlanAsync("What lamps are in the office?", []);

        Assert.Contains("Desk lamp", result.AssistantMessage);
        Assert.Contains("Ceiling light", result.AssistantMessage);
        Assert.Null(result.ProposedAction);
    }

    [Fact]
    public async Task PlanAsync_RequestTargetsZone_ReturnsZoneAction()
    {
        var zone = new HueResource("zone-upstairs", "Upstairs", "zone");
        var hue = new FakeHueService([zone]);
        var service = new HueConversationService(
            new FakeIntentParser(new HueIntent(HueOperation.TurnOn, "Upstairs", "zone")), hue);

        var result = await service.PlanAsync("Turn on upstairs", []);

        Assert.Equal("zone-upstairs", result.ProposedAction?.Target.Id);
        Assert.Equal(HueOperation.TurnOn, result.ProposedAction?.Operation);
    }

    [Fact]
    public async Task PlanAsync_ParserReturnsMultipleIntents_ReturnsActionForEachIntent()
    {
        var desk = new HueResource("light-desk", "Desk", "light");
        var ceiling = new HueResource("light-ceiling", "Ceiling", "light");
        var hue = new FakeHueService([desk, ceiling]);
        var parser = new FakeIntentParser(new HueIntent(HueOperation.TurnOn, "Desk", "light"),
            new HueIntent(HueOperation.TurnOn, "Ceiling", "light"));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk and Ceiling", []);
        Assert.NotNull(result.ProposedActions);
        var proposedActions = result.ProposedActions!;

        Assert.Equal(2, proposedActions.Count);
        Assert.Equal(["light-desk", "light-ceiling"], proposedActions.Select(action => action.Target.Id));
    }

    [Fact]
    public async Task PlanAsync_MultipleGroupedLightsAreRequested_ReturnsActionForEachNamedLight()
    {
        var office = new HueResourceGroup("room-office", "Office", "room");
        var desk = new HueResource("light-desk", "Desk", "light", [office]);
        var pinboard = new HueResource("light-pinboard", "Pinboard", "light", [office]);
        var hue = new FakeHueService([desk, pinboard]);
        var parser = new FakeIntentParser(
            new HueIntent(HueOperation.TurnOn, "Desk", "light", TargetGroupName: "Office", TargetGroupType: "room"),
            new HueIntent(HueOperation.TurnOn, "Pinboard", "light", TargetGroupName: "Office",
                TargetGroupType: "room"));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk and Pinboard", []);
        Assert.NotNull(result.ProposedActions);
        var proposedActions = result.ProposedActions!;

        Assert.Equal(["light-desk", "light-pinboard"], proposedActions.Select(action => action.Target.Id));
    }

    [Fact]
    public async Task PlanAsync_TargetIsUnknown_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([Office]);
        var service =
            new HueConversationService(new FakeIntentParser(new HueIntent(HueOperation.TurnOff, "Kitchen", "room")),
                hue);

        var result = await service.PlanAsync("Turn off kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_TargetNameIsDuplicated_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([Office, new HueResource("room-2", "Office", "room")]);
        var service =
            new HueConversationService(new FakeIntentParser(new HueIntent(HueOperation.TurnOn, "Office", "room")), hue);

        var result = await service.PlanAsync("Turn on the office", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("more than one", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_SceneNameIsDuplicatedAndRoomIsSpecified_ReturnsAssociatedSceneAction()
    {
        var bedroom = new HueResourceGroup("room-bedroom", "Bedroom", "room");
        var kitchen = new HueResourceGroup("room-kitchen", "Kitchen", "room");
        var scenes = new HueResource[]
        {
            new("scene-bedroom", "Relax", "scene", [bedroom]), new("scene-kitchen", "Relax", "scene", [kitchen])
        };
        var hue = new FakeHueService(scenes);
        var parser = new FakeIntentParser(new HueIntent(HueOperation.ActivateScene, "Relax", "scene",
            TargetGroupName: "Bedroom", TargetGroupType: "room"));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Activate Relax in the Bedroom", []);

        Assert.Equal("scene-bedroom", result.ProposedAction?.Target.Id);
        Assert.Contains("Bedroom", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_SceneNameIsDuplicatedWithoutRoom_ReturnsAvailableRoomClarification()
    {
        var hue = new FakeHueService([
            new HueResource("scene-bedroom", "Relax", "scene",
                [new HueResourceGroup("room-bedroom", "Bedroom", "room")]),
            new HueResource("scene-kitchen", "Relax", "scene",
                [new HueResourceGroup("room-kitchen", "Kitchen", "room")])
        ]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new HueIntent(HueOperation.ActivateScene, "Relax", "scene")), hue);

        var result = await service.PlanAsync("Activate Relax", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("Bedroom", result.AssistantMessage);
        Assert.Contains("Kitchen", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_SceneIsNotAssociatedWithRequestedRoom_ReturnsClarification()
    {
        var hue = new FakeHueService([
            new HueResource("scene-bedroom", "Relax", "scene",
                [new HueResourceGroup("room-bedroom", "Bedroom", "room")])
        ]);
        var service = new HueConversationService(
            new FakeIntentParser(new HueIntent(HueOperation.ActivateScene, "Relax", "scene", TargetGroupName: "Kitchen",
                TargetGroupType: "room")), hue);

        var result = await service.PlanAsync("Activate Relax in the Kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("associated with room ‘Kitchen’", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestNamesMultipleGroups_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([
            new HueResource("scene-bedroom", "Relax", "scene",
                [new HueResourceGroup("room-bedroom", "Bedroom", "room")]),
            new HueResource("scene-kitchen", "Relax", "scene",
                [new HueResourceGroup("room-kitchen", "Kitchen", "room")])
        ]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new HueIntent(HueOperation.ActivateScene, "Relax", "scene")), hue);

        var result = await service.PlanAsync("Activate Relax in Bedroom and Kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("multiple rooms or zones", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    private sealed class FakeIntentParser : IIntentParser
    {
        private readonly IReadOnlyList<HueIntent> _intents;

        public FakeIntentParser(HueIntent intent)
        {
            _intents = [intent];
        }

        public FakeIntentParser(HueIntent first, HueIntent second, params HueIntent[] additional)
        {
            _intents = [first, second, .. additional];
        }

        public Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
            IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_intents);
        }
    }

    private sealed class FakeHueService(IReadOnlyList<HueResource> resources) : IHueService
    {
        public List<HueAction> Executed { get; } = [];

        public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(resources);
        }

        public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
        {
            Executed.Add(action);
            return Task.CompletedTask;
        }
    }
}

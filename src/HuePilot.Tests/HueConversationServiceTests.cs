using HuePilot.Application;
using HuePilot.Application.Chat;
using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;

namespace HuePilot.Tests;

public sealed class HueConversationServiceTests
{
    private static readonly HueResource Office = new("room-1", "Office", HueResourceType.Room);

    [Fact]
    public async Task PlanAsync_RequestIsClear_ReturnsValidatedActionWithoutExecutingIt()
    {
        var hue = new FakeHueService([Office]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new SetBrightnessIntent("Office", HueResourceType.Room, 65)), hue);

        var result = await service.PlanAsync("Set the office to 65 percent", []);

        Assert.Equal(HueOperation.SetBrightness, result.ProposedActions!.Single().Operation);
        Assert.Equal(65, result.ProposedActions!.Single().BrightnessPercent);
        Assert.Empty(hue.Executed);
        Assert.Contains("65%", result.AssistantMessage);
        await service.ExecuteAsync(result.ProposedActions!.Single());
        Assert.Single(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestIsAmbiguous_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([Office]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new ClarificationIntent("Which room?")), hue);

        var result = await service.PlanAsync("Turn on the lights", []);

        Assert.True(result.NeedsClarification);
        Assert.Equal("Which room?", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestListsLightsInRoom_ReturnsAssociatedLights()
    {
        var office = new HueResourceGroup("room-1", "Office", HueResourceType.Room);
        var desk = new HueResource("light-1", "Desk lamp", HueResourceType.Light, [office]);
        var ceiling = new HueResource("light-2", "Ceiling light", HueResourceType.Light, [office]);
        var hue = new FakeHueService([Office, desk, ceiling]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new ListResourcesIntent("Office", HueResourceType.Light)), hue);

        var result = await service.PlanAsync("What lamps are in the office?", []);

        Assert.Contains("Desk lamp", result.AssistantMessage);
        Assert.Contains("Ceiling light", result.AssistantMessage);
        Assert.Null(result.ProposedActions);
    }

    [Fact]
    public async Task PlanAsync_RequestTargetsZone_ReturnsZoneAction()
    {
        var zone = new HueResource("zone-upstairs", "Upstairs", HueResourceType.Zone);
        var hue = new FakeHueService([zone]);
        var service = new HueConversationService(
            new FakeIntentParser(new TurnOnIntent("Upstairs", HueResourceType.Zone)), hue);

        var result = await service.PlanAsync("Turn on upstairs", []);

        Assert.Equal("zone-upstairs", result.ProposedActions!.Single().Target.Id);
        Assert.Equal(HueOperation.TurnOn, result.ProposedActions!.Single().Operation);
    }

    [Fact]
    public async Task PlanAsync_ParserReturnsMultipleIntents_ReturnsActionForEachIntent()
    {
        var desk = new HueResource("light-desk", "Desk", HueResourceType.Light);
        var ceiling = new HueResource("light-ceiling", "Ceiling", HueResourceType.Light);
        var hue = new FakeHueService([desk, ceiling]);
        var parser = new FakeIntentParser(new TurnOnIntent("Desk", HueResourceType.Light),
            new TurnOnIntent("Ceiling", HueResourceType.Light));
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
        var office = new HueResourceGroup("room-office", "Office", HueResourceType.Room);
        var desk = new HueResource("light-desk", "Desk", HueResourceType.Light, [office]);
        var pinboard = new HueResource("light-pinboard", "Pinboard", HueResourceType.Light, [office]);
        var hue = new FakeHueService([desk, pinboard]);
        var parser = new FakeIntentParser(
            new TurnOnIntent("Desk", HueResourceType.Light, TargetGroupName: "Office", TargetGroupType: HueResourceType.Room),
            new TurnOnIntent("Pinboard", HueResourceType.Light, TargetGroupName: "Office",
                TargetGroupType: HueResourceType.Room));
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
            new HueConversationService(new FakeIntentParser(new TurnOffIntent("Kitchen", HueResourceType.Room)),
                hue);

        var result = await service.PlanAsync("Turn off kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_TargetNameIsDuplicated_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([Office, new HueResource("room-2", "Office", HueResourceType.Room)]);
        var service =
            new HueConversationService(new FakeIntentParser(new TurnOnIntent("Office", HueResourceType.Room)), hue);

        var result = await service.PlanAsync("Turn on the office", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("more than one", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_SceneNameIsDuplicatedAndRoomIsSpecified_ReturnsAssociatedSceneAction()
    {
        var bedroom = new HueResourceGroup("room-bedroom", "Bedroom", HueResourceType.Room);
        var kitchen = new HueResourceGroup("room-kitchen", "Kitchen", HueResourceType.Room);
        var scenes = new HueResource[]
        {
            new("scene-bedroom", "Relax", HueResourceType.Scene, [bedroom]), new("scene-kitchen", "Relax", HueResourceType.Scene, [kitchen])
        };
        var hue = new FakeHueService(scenes);
        var parser = new FakeIntentParser(new ActivateSceneIntent("Relax", HueResourceType.Scene,
            TargetGroupName: "Bedroom", TargetGroupType: HueResourceType.Room));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Activate Relax in the Bedroom", []);

        Assert.Equal("scene-bedroom", result.ProposedActions!.Single().Target.Id);
        Assert.Contains("Bedroom", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_SceneNameIsDuplicatedWithoutRoom_ReturnsAvailableRoomClarification()
    {
        var hue = new FakeHueService([
            new HueResource("scene-bedroom", "Relax", HueResourceType.Scene,
                [new HueResourceGroup("room-bedroom", "Bedroom", HueResourceType.Room)]),
            new HueResource("scene-kitchen", "Relax", HueResourceType.Scene,
                [new HueResourceGroup("room-kitchen", "Kitchen", HueResourceType.Room)])
        ]);
        var service =
            new HueConversationService(
                new FakeIntentParser(new ActivateSceneIntent("Relax", HueResourceType.Scene)), hue);

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
            new HueResource("scene-bedroom", "Relax", HueResourceType.Scene,
                [new HueResourceGroup("room-bedroom", "Bedroom", HueResourceType.Room)])
        ]);
        var service = new HueConversationService(
            new FakeIntentParser(new ActivateSceneIntent("Relax", HueResourceType.Scene, TargetGroupName: "Kitchen",
                TargetGroupType: HueResourceType.Room)), hue);

        var result = await service.PlanAsync("Activate Relax in the Kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("associated with room ‘Kitchen’", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestNamesMultipleGroups_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([
            new HueResource("scene-bedroom", "Relax", HueResourceType.Scene,
                [new HueResourceGroup("room-bedroom", "Bedroom", HueResourceType.Room)]),
            new HueResource("scene-kitchen", "Relax", HueResourceType.Scene,
                [new HueResourceGroup("room-kitchen", "Kitchen", HueResourceType.Room)])
        ]);
        var service =
                new HueConversationService(
                new FakeIntentParser(new ActivateSceneIntent("Relax", HueResourceType.Scene)), hue);

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

using System.Text.Json;
using HuePilot.Application.Chat;
using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;
using HuePilot.Application.IntentParsing;

namespace HuePilot.Tests;

public sealed class HueConversationServiceTests
{
    private const string KnownLightUuid = "e96b3aa8-2319-45a2-b3fb-a310252cc001";
    private const string DuplicateUuid = "e96b3aa8-2319-45a2-b3fb-a310252cc002";
    private const string MissingTargetUuid = "e96b3aa8-2319-45a2-b3fb-a310252cc003";
    private const string MissingGroupUuid = "e96b3aa8-2319-45a2-b3fb-a310252cc004";
    private const string EmptyGroupUuid = "e96b3aa8-2319-45a2-b3fb-a310252cc005";

    private static readonly HueResourceGroup Office = new(Guid.NewGuid(), "Office", HueResourceType.Room, []);

    [Fact]
    public async Task PlanAsync_RequestIsClear_ReturnsValidatedActionWithoutExecutingIt()
    {
        var hue = new FakeHueService([Office]);
        var service = new HueConversationService(new FakeIntentParser(new SetBrightnessIntent(Office.Id, 65)), hue);

        var result = await service.PlanAsync("Set the office to 65 percent", []);

        Assert.Equal(HueOperation.SetBrightness, result.ProposedActions!.Single().Operation);
        Assert.Equal(65, result.ProposedActions!.Single().BrightnessPercent);
        Assert.Empty(hue.Executed);
        Assert.Contains("65%", result.AssistantMessage);
        await service.ExecuteAsync(result.ProposedActions!.Single());
        Assert.Single(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_UsesTheGroundedIdDespiteIdenticalDisplayNames()
    {
        var first = new HueResource(Guid.NewGuid(), "Desk", HueResourceType.Light);
        var second = first with { Id = Guid.NewGuid() };
        var service = new HueConversationService(new FakeIntentParser(new TurnOnIntent(second.Id)),
            new FakeHueService([first, second]));

        var plan = await service.PlanAsync("Use my selected lamp", []);

        Assert.Same(second, Assert.Single(plan.ProposedActions!).Target);
    }

    [Theory]
    [InlineData(MissingTargetUuid, null)]
    [InlineData(DuplicateUuid, null)]
    [InlineData(KnownLightUuid, MissingGroupUuid)]
    [InlineData(KnownLightUuid, EmptyGroupUuid)]
    [InlineData(KnownLightUuid, KnownLightUuid)]
    public async Task PlanAsync_InvalidTargetOrScopeId_DiscardsEarlierActions(string id, string? groupId)
    {
        var light = new HueResource(new Guid(KnownLightUuid), "Desk", HueResourceType.Light);
        var duplicate = light with { Id = new Guid(DuplicateUuid) };
        var hue = new FakeHueService([
            light, duplicate, duplicate,
            new HueResourceGroup(new Guid(EmptyGroupUuid), "Office", HueResourceType.Room, [])
        ]);
        var service = new HueConversationService(
            new FakeIntentParser(new TurnOnIntent(light.Id),
                new TurnOffIntent(Guid.Parse(id), groupId is null ? null : Guid.Parse(groupId))), hue);

        var plan = await service.PlanAsync("Apply the commands", []);

        Assert.True(plan.NeedsClarification);
        Assert.Null(plan.ProposedActions);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestIsAmbiguous_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([Office]);
        var service = new HueConversationService(new FakeIntentParser(new ClarificationIntent("Which room?")), hue);

        var result = await service.PlanAsync("Turn on the lights", []);

        Assert.True(result.NeedsClarification);
        Assert.Equal("Which room?", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestListsLightsInRoom_ReturnsAssociatedLights()
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var ceiling = new HueResource(Guid.NewGuid(), "Ceiling light", HueResourceType.Light);
        var otherLamp = new HueResource(Guid.NewGuid(), "Floor lamp", HueResourceType.Light);
        var office = Office with { Resources = [desk, ceiling] };
        var hue = new FakeHueService([office, desk, ceiling, otherLamp]);
        var service =
            new HueConversationService(new FakeIntentParser(new ListResourcesIntent(HueResourceType.Light, Office.Id)),
                hue);

        var result = await service.PlanAsync("What lamps are in the office?", []);

        Assert.Contains("Desk lamp", result.AssistantMessage);
        Assert.Contains("Ceiling light", result.AssistantMessage);
        Assert.DoesNotContain("Floor lamp", result.AssistantMessage);
        Assert.Null(result.ProposedActions);
    }

    [Theory]
    [InlineData(HueResourceType.Room, "Office", "Desk lamp", "Floor lamp")]
    [InlineData(HueResourceType.Zone, "Upstairs", "Floor lamp", "Desk lamp")]
    public async Task PlanAsync_ListScopeHasGroupType_ReturnsOnlyMatchingGroupMembers(HueResourceType groupType,
        string groupName, string expectedName, string excludedName)
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var floor = new HueResource(Guid.NewGuid(), "Floor lamp", HueResourceType.Light);
        var officeRoom = Office with { Resources = [desk] };
        var upstairs = new HueResourceGroup(Guid.NewGuid(), "Upstairs", HueResourceType.Zone, [floor]);
        var hue = new FakeHueService([officeRoom, upstairs, desk, floor]);
        var parser = new FakeIntentParser(new ListResourcesIntent(HueResourceType.Light,
            groupType == HueResourceType.Room ? officeRoom.Id : upstairs.Id));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync($"What lights are in the {groupName} {groupType.ToHueV2WireType()}?", []);

        Assert.False(result.NeedsClarification);
        Assert.Contains(expectedName, result.AssistantMessage);
        Assert.DoesNotContain(excludedName, result.AssistantMessage);
    }

    [Theory]
    [InlineData(HueResourceType.Room, "Rooms", "Office")]
    [InlineData(HueResourceType.Zone, "Zones", "Upstairs")]
    public async Task PlanAsync_ListsNamedGroup_ReturnsMatchingGroupItself(HueResourceType groupType, string typeLabel,
        string groupName)
    {
        var upstairs = new HueResourceGroup(Guid.NewGuid(), "Upstairs", HueResourceType.Zone, []);
        var hue = new FakeHueService([Office, upstairs]);
        var parser = new FakeIntentParser(new ListResourcesIntent(groupType,
            groupType == HueResourceType.Room ? Office.Id : upstairs.Id));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync($"List the {groupName} {groupType.ToHueV2WireType()}", []);

        Assert.False(result.NeedsClarification);
        Assert.StartsWith(typeLabel, result.AssistantMessage);
        Assert.Contains($": {groupName}.", result.AssistantMessage);
    }

    [Theory]
    [InlineData(HueResourceType.Zone, null)]
    [InlineData(HueResourceType.Zone, HueResourceType.Room)]
    [InlineData(HueResourceType.Room, HueResourceType.Room)]
    public async Task PlanAsync_ListScopeMatchesMultipleGroups_ReturnsClarification(HueResourceType otherGroupType,
        HueResourceType? requestedGroupType)
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var office = Office with { Resources = [desk] };
        var otherOffice = new HueResourceGroup(Guid.NewGuid(), "Office", otherGroupType, []);
        var hue = new FakeHueService([office, otherOffice, desk]);
        var parser = new StructuredParser("ListResources", null, HueResourceType.Light, "Office", requestedGroupType);
        var service = new HueConversationService(parser, hue);

        var typeQualifier = requestedGroupType is { } groupType ? $" {groupType.ToHueV2WireType()}" : "";
        var result = await service.PlanAsync($"What lights are in the Office{typeQualifier}?", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("Office", result.AssistantMessage);
    }

    [Fact]
    public async Task PlanAsync_RequestTargetsZone_ReturnsZoneAction()
    {
        var zone = new HueResourceGroup(Guid.NewGuid(), "Upstairs", HueResourceType.Zone, []);
        var hue = new FakeHueService([zone]);
        var service = new HueConversationService(new FakeIntentParser(new TurnOnIntent(zone.Id)), hue);

        var result = await service.PlanAsync("Turn on upstairs", []);

        Assert.Equal(zone.Id, result.ProposedActions!.Single().Target.Id);
        Assert.Equal(HueOperation.TurnOn, result.ProposedActions!.Single().Operation);
    }

    [Fact]
    public async Task PlanAsync_ParserReturnsMultipleIntents_ReturnsActionForEachIntent()
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk", HueResourceType.Light);
        var ceiling = new HueResource(Guid.NewGuid(), "Ceiling", HueResourceType.Light);
        var hue = new FakeHueService([desk, ceiling]);
        var parser = new FakeIntentParser(new TurnOnIntent(desk.Id), new TurnOnIntent(ceiling.Id));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk and Ceiling", []);
        Assert.NotNull(result.ProposedActions);
        var proposedActions = result.ProposedActions!;

        Assert.Equal(2, proposedActions.Count);
        Assert.Equal([desk.Id, ceiling.Id], proposedActions.Select(action => action.Target.Id));
    }

    [Fact]
    public async Task PlanAsync_MultipleGroupedLightsAreRequested_ReturnsActionForEachNamedLight()
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk", HueResourceType.Light);
        var pinboard = new HueResource(Guid.NewGuid(), "Pinboard", HueResourceType.Light);
        var office = new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, [desk, pinboard]);
        var hue = new FakeHueService([office, desk, pinboard]);
        var parser = new FakeIntentParser(
            new TurnOnIntent(desk.Id, office.Id), new TurnOnIntent(pinboard.Id, office.Id));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk and Pinboard", []);
        Assert.NotNull(result.ProposedActions);
        var proposedActions = result.ProposedActions!;

        Assert.Equal([desk.Id, pinboard.Id], proposedActions.Select(action => action.Target.Id));
    }

    [Fact]
    public async Task PlanAsync_TargetIsUnknown_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([Office]);
        var service = new HueConversationService(new FakeIntentParser(new TurnOffIntent(Guid.NewGuid())), hue);

        var result = await service.PlanAsync("Turn off kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_TargetNameIsDuplicated_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService([
            Office, new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, [])
        ]);
        var service = new HueConversationService(new StructuredParser("TurnOn", "Office", HueResourceType.Room), hue);

        var result = await service.PlanAsync("Turn on the office", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("more than one", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_SceneNameIsDuplicatedAndRoomIsSpecified_ReturnsAssociatedSceneAction()
    {
        var catalog = CreateRelaxSceneCatalog();
        var bedroom = catalog.OfType<HueResourceGroup>().Single(group => group.Name == "Bedroom");
        var scene = Assert.Single(bedroom.Resources);
        var hue = new FakeHueService(catalog);
        var parser = new FakeIntentParser(new ActivateSceneIntent(scene.Id, bedroom.Id));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Activate Relax in the Bedroom", []);

        Assert.Equal(scene.Id, result.ProposedActions!.Single().Target.Id);
        Assert.Contains("Bedroom", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Theory]
    [InlineData("Activate Relax in the Office room")]
    [InlineData("Activate Relax in the Office")]
    public async Task PlanAsync_ExplicitGroupNameMatchesRoomAndZone_RequiresClarification(string request)
    {
        var relax = new HueResource(Guid.NewGuid(), "Relax", HueResourceType.Scene);
        var officeRoom = Office with { Resources = [relax] };
        var officeZone = new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Zone, []);
        var hue = new FakeHueService([officeRoom, officeZone, relax]);
        var parser = new StructuredParser("ActivateScene", "Relax", HueResourceType.Scene, "Office",
            HueResourceType.Room);
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync(request, []);

        Assert.True(result.NeedsClarification);
        Assert.Null(result.ProposedActions);
        Assert.Empty(hue.Executed);
    }

    [Theory]
    [InlineData("Relax", "Activate Relax in the Office, not in the Office zone")]
    [InlineData("Office Zone", "Activate Office Zone in the Office")]
    [InlineData("Relax in Office Zone", "Activate Relax in Office Zone")]
    [InlineData("Relax", "Activate Relax in the Office zone")]
    public async Task PlanAsync_GroupQualifierCannotResolveDuplicateGroupName(string sceneName, string request)
    {
        var scene = new HueResource(Guid.NewGuid(), sceneName, HueResourceType.Scene);
        var officeZone = new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Zone, [scene]);
        var hue = new FakeHueService([Office, officeZone, scene]);
        var parser = new StructuredParser("ActivateScene", sceneName, HueResourceType.Scene, "Office",
            HueResourceType.Zone);
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync(request, []);

        Assert.True(result.NeedsClarification);
        Assert.Null(result.ProposedActions);
    }

    [Theory]
    [InlineData("Turn on Desk lamp and list the lights in Greenhouse")]
    [InlineData("Turn on Desk lamp and list Greenhouse lights")]
    [InlineData("Turn on Desk lamp and list the Office lights")]
    [InlineData("Turn on Desk lamp and list all lights in the Greenhouse")]
    [InlineData("Turn on Desk lamp and list all lights, in the Greenhouse only")]
    [InlineData("Turn on Desk lamp and list all lights and scenes in the Greenhouse")]
    public async Task PlanAsync_MutationWithListWithoutGroupMetadata_BlocksActions(string request)
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var hue = new FakeHueService([desk, Office with { Resources = [desk] }]);
        var parser = new FakeIntentParser(new TurnOnIntent(desk.Id), new ListResourcesIntent(HueResourceType.Light));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync(request, []);

        Assert.True(result.NeedsClarification);
        Assert.Null(result.ProposedActions);
        Assert.Empty(hue.Executed);
    }

    [Theory]
    [InlineData("Turn on Office and list all lights")]
    [InlineData("List all lights and turn on Office")]
    public async Task PlanAsync_ExplicitGlobalListAndMutation_ListsEveryLightAndPlansTheAction(string request)
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk", HueResourceType.Light);
        var hall = new HueResource(Guid.NewGuid(), "Hall lamp", HueResourceType.Light);
        var service = new HueConversationService(
            new FakeIntentParser(new TurnOnIntent(Office.Id), new ListResourcesIntent(HueResourceType.Light)),
            new FakeHueService([Office, desk, hall]));

        var result = await service.PlanAsync(request, []);

        Assert.False(result.NeedsClarification);
        Assert.Equal(Office.Id, Assert.Single(result.ProposedActions!).Target.Id);
        Assert.Contains("Desk", result.AssistantMessage);
        Assert.Contains("Hall lamp", result.AssistantMessage);
    }


    [Fact]
    public async Task PlanAsync_MutationWithExplicitlyScopedList_ReturnsValidatedAction()
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var hue = new FakeHueService([desk, Office with { Resources = [desk] }]);
        var parser = new FakeIntentParser(new TurnOnIntent(desk.Id),
            new ListResourcesIntent(HueResourceType.Light, Office.Id));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk lamp and list the Office lights", []);

        Assert.False(result.NeedsClarification);
        Assert.Equal(desk.Id, Assert.Single(result.ProposedActions!).Target.Id);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_ListScopeIsUnknown_DiscardsPreviouslyProposedAction()
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var hue = new FakeHueService([desk, Office]);
        var parser = new FakeIntentParser(new TurnOnIntent(desk.Id),
            new ListResourcesIntent(HueResourceType.Light, Guid.NewGuid()));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("Turn on Desk lamp and list the lights in the Greenhouse", []);

        Assert.True(result.NeedsClarification);
        Assert.Null(result.ProposedActions);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_StandaloneUnscopedList_ListsLights()
    {
        var desk = new HueResource(Guid.NewGuid(), "Desk lamp", HueResourceType.Light);
        var service = new HueConversationService(new FakeIntentParser(new ListResourcesIntent(HueResourceType.Light)),
            new FakeHueService([desk]));

        var result = await service.PlanAsync("List all lights", []);

        Assert.False(result.NeedsClarification);
        Assert.Contains("Desk lamp", result.AssistantMessage);
    }

    [Fact]
    public async Task PlanAsync_ListScopeIsKnownAndEmpty_ReturnsEmptyList()
    {
        var hue = new FakeHueService([Office]);
        var parser = new FakeIntentParser(new ListResourcesIntent(HueResourceType.Light, Office.Id));
        var service = new HueConversationService(parser, hue);

        var result = await service.PlanAsync("What lights are in the Office?", []);

        Assert.False(result.NeedsClarification);
        Assert.Contains("No lights were found in Office", result.AssistantMessage);
    }

    [Fact]
    public async Task PlanAsync_SceneNameIsDuplicatedWithoutRoom_ReturnsAvailableRoomClarification()
    {
        var hue = new FakeHueService(CreateRelaxSceneCatalog());
        var service =
            new HueConversationService(new StructuredParser("ActivateScene", "Relax", HueResourceType.Scene), hue);

        var result = await service.PlanAsync("Activate Relax", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("Bedroom", result.AssistantMessage);
        Assert.Contains("Kitchen", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Theory]
    [InlineData("Kitchen")]
    [InlineData(null)]
    [InlineData("")]
    public async Task PlanAsync_RequestNamesRoomWithoutScene_ReturnsClarification(string? parserGroupName)
    {
        var bedroomScene = new HueResource(Guid.NewGuid(), "Relax", HueResourceType.Scene);
        var bedroom = new HueResourceGroup(Guid.NewGuid(), "Bedroom", HueResourceType.Room, [bedroomScene]);
        var kitchen = new HueResourceGroup(Guid.NewGuid(), "Kitchen", HueResourceType.Room, []);
        var hue = new FakeHueService([bedroom, kitchen, bedroomScene]);
        var service = new HueConversationService(
            new StructuredParser("ActivateScene", "Relax", HueResourceType.Scene, parserGroupName), hue);

        var result = await service.PlanAsync("Activate Relax in the Kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("associated with room ‘Kitchen’", result.AssistantMessage);
        Assert.Null(result.ProposedActions);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_RequestNamesMultipleGroups_ReturnsClarificationWithoutExecutingAction()
    {
        var hue = new FakeHueService(CreateRelaxSceneCatalog());
        var service =
            new HueConversationService(new StructuredParser("ActivateScene", "Relax", HueResourceType.Scene), hue);

        var result = await service.PlanAsync("Activate Relax in Bedroom and Kitchen", []);

        Assert.True(result.NeedsClarification);
        Assert.Contains("multiple rooms or zones", result.AssistantMessage);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PlanAsync_FollowUpResolvesToDuplicateRoomName_ReturnsClarification()
    {
        var relax = new HueResource(Guid.NewGuid(), "Relax", HueResourceType.Scene);
        var office = Office with { Resources = [relax] };
        var otherOffice = Office with { Id = Guid.NewGuid() };
        var hue = new FakeHueService([office, otherOffice, relax]);
        var parser = new StructuredParser("ActivateScene", "Relax", HueResourceType.Scene, "Office",
            HueResourceType.Room);
        var service = new HueConversationService(parser, hue);
        var history = new ChatTurn[] { new("user", "Let's work with the Office") };

        var result = await service.PlanAsync("Activate Relax there", history);

        Assert.True(result.NeedsClarification);
        Assert.Contains("Office", result.AssistantMessage);
        Assert.Null(result.ProposedActions);
        Assert.Empty(hue.Executed);
    }

    private static IReadOnlyList<HueResource> CreateRelaxSceneCatalog()
    {
        var bedroomScene = new HueResource(Guid.NewGuid(), "Relax", HueResourceType.Scene);
        var kitchenScene = new HueResource(Guid.NewGuid(), "Relax", HueResourceType.Scene);

        return
        [
            new HueResourceGroup(Guid.NewGuid(), "Bedroom", HueResourceType.Room, [bedroomScene]),
            new HueResourceGroup(Guid.NewGuid(), "Kitchen", HueResourceType.Room, [kitchenScene]), bedroomScene,
            kitchenScene
        ];
    }

    // Feed deterministic model output through the real parser for language-resolution regressions.
    private sealed class StructuredParser(
        string operation,
        string? targetName,
        HueResourceType targetType,
        string? targetGroupName = null,
        HueResourceType? targetGroupType = null) : IIntentParser
    {
        public Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
            IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(new
            {
                operation,
                targetName,
                targetType = targetType.ToHueV2WireType(),
                targetGroupName,
                targetGroupType = targetGroupType?.ToHueV2WireType()
            });
            return Task.FromResult(OpenAiStructuredIntentParser.ParseIntents(json, request, availableResources));
        }
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
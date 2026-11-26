using System.Text.Json;
using HuePilot.Application.Chat;
using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;
using HuePilot.Application.IntentParsing;

namespace HuePilot.Tests;

public sealed class OpenAiToolCallingIntentParserTests
{
    private const string DeskUuid = "1f4a3e82-0d92-4a22-860a-095ab64d0791";
    private const string CeilingUuid = "a4479e32-4a2e-40ec-89d9-65c8456736f9";
    private const string KitchenDeskUuid = "490f124b-0943-402d-9494-b138a82d4ca1";
    private const string OfficeUuid = "45eaacb4-3961-4635-833a-5546923f297b";
    private const string KitchenUuid = "f3269e13-3e4b-4851-a2c9-f793b4f88115";
    private const string UpstairsUuid = "aa138c49-0b21-4b03-8106-47ce0528ee51";
    private const string OtherOfficeUuid = "65735f01-8b99-41f3-b15c-630f957c1771";
    private static readonly HueResource OfficeDesk = new(new Guid(DeskUuid), "Desk lamp", HueResourceType.Light);

    private static readonly HueResource OfficeCeiling =
        new(new Guid(CeilingUuid), "Ceiling light", HueResourceType.Light);

    private static readonly HueResource
        KitchenDesk = new(new Guid(KitchenDeskUuid), "Desk lamp", HueResourceType.Light);

    private static readonly HueResource[] Resources =
    [
        new HueResourceGroup(new Guid(OfficeUuid), "Office", HueResourceType.Room, [OfficeDesk, OfficeCeiling]),
        new HueResourceGroup(new Guid(KitchenUuid), "Kitchen", HueResourceType.Room, [KitchenDesk]),
        new HueResourceGroup(new Guid(UpstairsUuid), "Upstairs", HueResourceType.Zone, [OfficeDesk]), OfficeDesk,
        OfficeCeiling, KitchenDesk
    ];

    [Fact]
    public void LookupResources_MatchesAnUppercaseGroupUuid()
    {
        var office = new HueResourceGroup(new Guid(OfficeUuid), "Office", HueResourceType.Room, [OfficeDesk]);
        var arguments = JsonSerializer.Serialize(new
        {
            query = "", resourceType = "light", groupName = "", groupId = OfficeUuid.ToUpperInvariant()
        });

        var json = OpenAiToolCallingIntentParser.LookupResources(arguments, [OfficeDesk, office]);

        using var document = JsonDocument.Parse(json);
        Assert.Single(document.RootElement.GetProperty("resources").EnumerateArray());
    }

    [Fact]
    public void LookupResources_QueryMatchesRoomName_ReturnsItsMemberLights()
    {
        var json = OpenAiToolCallingIntentParser.LookupResources(
            """{"query":"Office","resourceType":"light","groupName":"","groupId":""}""", Resources);
        using var document = JsonDocument.Parse(json);
        var lightIds = document.RootElement.GetProperty("resources").EnumerateArray()
            .Select(light => light.GetProperty("id").GetString());

        Assert.Equal([DeskUuid, CeilingUuid], lightIds);
    }

    [Theory]
    [InlineData("Desk lamp", "Office", "")]
    [InlineData("Desk lamp", "", OfficeUuid)]
    public void LookupResources_ScopedLightSearch_ReturnsExactMemberAndItsGroups(string query, string groupName,
        string groupId)
    {
        var arguments = JsonSerializer.Serialize(new { query, resourceType = "light", groupName, groupId });
        var json = OpenAiToolCallingIntentParser.LookupResources(arguments, Resources);
        using var document = JsonDocument.Parse(json);
        var match = Assert.Single(document.RootElement.GetProperty("resources").EnumerateArray());

        Assert.Equal(DeskUuid, match.GetProperty("id").GetString());
        Assert.Collection(match.GetProperty("groups").EnumerateArray(),
            room => AssertGroup(room, OfficeUuid, "Office", HueResourceType.Room),
            zone => AssertGroup(zone, UpstairsUuid, "Upstairs", HueResourceType.Zone));
        var group = Assert.Single(document.RootElement.GetProperty("matchedGroups").EnumerateArray());
        AssertGroup(group, OfficeUuid, "Office", HueResourceType.Room);
    }

    [Fact]
    public void ParseProposal_ExactTargetBelongsToRequestedGroup_ReturnsGroupScope()
    {
        var intent = Assert.IsType<TurnOnIntent>(ParseProposal(DeskUuid, OfficeUuid,
            request: "Turn on the Desk lamp in Office"));

        Assert.Equal(OfficeDesk.Id, intent.TargetResourceId);
        Assert.Equal(new Guid(OfficeUuid), intent.TargetGroupId);
    }

    [Fact]
    public void ParseProposal_ProviderAloneSelectsGroupForDuplicateLight_AsksUser()
    {
        var intent = ParseProposal(DeskUuid, OfficeUuid, request: "Turn on the Desk lamp");

        Assert.IsType<ClarificationIntent>(intent);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, OfficeUuid, OfficeUuid)]
    [InlineData(OfficeUuid, null, OfficeUuid)]
    public void ParseProposal_ListCategoryHasOptionalGroupId(string? targetId, string? groupId, string? expectedScope)
    {
        var json = JsonSerializer.Serialize(new
        {
            operation = "ListResources", targetType = "light", targetId, targetGroupId = groupId
        });
        var intent = OpenAiToolCallingIntentParser.ParseProposal(json, new HueIntentResolver(Resources, ""));

        Assert.Equal(
            new ListResourcesIntent(HueResourceType.Light, expectedScope is null ? null : Guid.Parse(expectedScope)),
            intent);
    }

    [Fact]
    public void ParseProposal_OnlySameNamedLightBelongsToRequestedGroup_RequiresClarification()
    {
        Assert.IsType<ClarificationIntent>(ParseProposal(KitchenDeskUuid, OfficeUuid));
    }

    [Theory]
    [InlineData(DeskUuid, false)]
    [InlineData(KitchenDeskUuid, true)]
    public async Task PlanAsync_ValidatedDeskIdMustMatchUserRoom(string targetId, bool needsClarification)
    {
        var service = new HueConversationService(new ProposalParser(targetId), new CatalogHueService());

        var plan = await service.PlanAsync("Turn on the Desk lamp in the Office", []);

        Assert.Equal(needsClarification, plan.NeedsClarification);
        if (needsClarification)
        {
            Assert.Null(plan.ProposedActions);
        }
        else
        {
            Assert.Equal(new Guid(targetId), Assert.Single(plan.ProposedActions!).Target.Id);
        }
    }


    [Theory]
    [InlineData(OfficeUuid, OfficeUuid)]
    [InlineData(OfficeUuid, OtherOfficeUuid)]
    [InlineData(OfficeUuid + "-extra", OfficeUuid)]
    public async Task PlanAsync_DuplicateGroupNamesRemainAmbiguousWithProposalId(string userGroupId,
        string proposedGroupId)
    {
        var otherOffice = new HueResourceGroup(new Guid(OtherOfficeUuid), "Office", HueResourceType.Room, [OfficeDesk]);
        var service = new HueConversationService(new ProposalParser(DeskUuid, proposedGroupId),
            new CatalogHueService([.. Resources, otherOffice]));

        var plan = await service.PlanAsync($"Turn on the Desk lamp in Office ({userGroupId})", []);

        Assert.True(plan.NeedsClarification);
        Assert.Null(plan.ProposedActions);
    }

    [Theory]
    [InlineData("Desk lamp")]
    [InlineData("Another light")]
    public void ParseProposal_TargetIdIsDuplicated_RequiresClarificationEvenWithMatchingName(string duplicateName)
    {
        var duplicate = OfficeDesk with { Name = duplicateName };

        Assert.IsType<ClarificationIntent>(ParseProposal(DeskUuid, null, [OfficeDesk, duplicate]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseProposal_GroupIdIsDuplicated_RequiresClarificationBeforeMembership(bool bothGroupsContainTarget)
    {
        var duplicate = new HueResourceGroup(new Guid(OfficeUuid), "Another office", HueResourceType.Room,
            bothGroupsContainTarget ? [OfficeDesk] : []);

        Assert.IsType<ClarificationIntent>(ParseProposal(DeskUuid, OfficeUuid, [.. Resources, duplicate]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData(" " + OfficeUuid + " ")]
    public void ParseProposal_GroupIdIsPresentButDoesNotMatchExactly_RequiresClarification(string targetGroupId)
    {
        Assert.IsType<ClarificationIntent>(ParseProposal(DeskUuid, targetGroupId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing-light")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData(OtherOfficeUuid)]
    public void ParseProposal_IdDoesNotIdentifyAnAvailableTarget_RequiresClarification(string? targetId)
    {
        var intent = ParseProposal(targetId, null, [OfficeDesk]);

        Assert.IsType<ClarificationIntent>(intent);
    }

    private static void AssertGroup(JsonElement group, string id, string name, HueResourceType type)
    {
        Assert.Equal(id, group.GetProperty("id").GetString());
        Assert.Equal(name, group.GetProperty("name").GetString());
        Assert.Equal((int)type, group.GetProperty("type").GetInt32());
    }

    private static HueIntent ParseProposal(string? targetId, string? targetGroupId,
        IReadOnlyList<HueResource>? resources = null, string request = "")
    {
        var arguments =
            JsonSerializer.Serialize(new { operation = "TurnOn", targetId, targetName = "Desk lamp", targetGroupId });
        return OpenAiToolCallingIntentParser.ParseProposal(arguments,
            new HueIntentResolver(resources ?? Resources, request));
    }

    private sealed class ProposalParser(string targetId, string? targetGroupId = null) : IIntentParser
    {
        public Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
            IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<HueIntent>>([
                ParseProposal(targetId, targetGroupId, availableResources, request)
            ]);
        }
    }

    private sealed class CatalogHueService(IReadOnlyList<HueResource>? resources = null) : IHueService
    {
        public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(resources ?? Resources);
        }

        public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Planning cannot execute Hue actions.");
        }
    }
}
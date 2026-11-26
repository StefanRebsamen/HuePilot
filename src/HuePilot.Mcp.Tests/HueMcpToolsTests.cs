using System.Net.Sockets;
using System.Text.Json;
using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;
using HuePilot.Mcp;

namespace HuePilot.Tests;

public sealed class HueMcpToolsTests
{
    private static readonly HueResource Desk = new(Guid.NewGuid(), "Desk", HueResourceType.Light);
    private static readonly HueResource Relax = new(Guid.NewGuid(), "Relax", HueResourceType.Scene);

    private static readonly HueResourceGroup
        Office = new(Guid.NewGuid(), "Office", HueResourceType.Room, [Desk, Relax]);

    [Fact]
    public async Task ListHueResources_ResourcesAreDiscovered_ReturnsTheirNamesTypesAndIds()
    {
        var hue = new FakeHueService([Desk, Office, Relax]);

        var output = await HueMcpTools.ListHueResources(hueService: hue);

        Assert.Contains("Desk", output);
        Assert.Contains(Desk.Id.ToString(), output);
        Assert.Contains("Relax", output);
        Assert.Contains("Office", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task ListHueResources_InternalHueV2TypeIsRequested_ReturnsFilterValidationError()
    {
        var output = await HueMcpTools.ListHueResources("device", new FakeHueService([Desk]));

        Assert.Contains("type must be all, room, zone, light, or scene", output);
    }

    [Fact]
    public async Task ListHueResources_FilteredLight_IncludesAllContainingGroups()
    {
        var upstairs = new HueResourceGroup(Guid.NewGuid(), "Upstairs", HueResourceType.Zone, [Desk]);
        var kitchen = new HueResourceGroup(Guid.NewGuid(), "Kitchen", HueResourceType.Room, []);
        var hue = new FakeHueService([Desk, Office, upstairs, kitchen, Relax]);

        using var output = JsonDocument.Parse(await HueMcpTools.ListHueResources("light", hue));

        var light = Assert.Single(output.RootElement.EnumerateArray());
        Assert.Equal(Desk.Id, light.GetProperty("Id").GetGuid());
        Assert.Equal([Office.Id, upstairs.Id],
            light.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("Id").GetGuid()));
    }

    [Fact]
    public async Task GetHueStatus_DiscoveredLight_ReturnsCurrentStatus()
    {
        var hue = new FakeHueService([Desk],
            status: new HueStatus(Desk.Id, "Desk", HueResourceType.Light, true, 40, 0.3127, 0.3290));

        var output = await HueMcpTools.GetHueStatus(Desk.Id.ToString(), hue);

        Assert.Contains("\"IsOn\": true", output);
        Assert.Contains("\"BrightnessPercent\": 40", output);
        Assert.Contains("\"ColorX\": 0.3127", output);
        Assert.Equal(Desk, Assert.Single(hue.StatusRequests));
    }

    [Fact]
    public async Task GetHueStatus_DuplicateResourceId_SkipsStatusRequest()
    {
        var hue = new FakeHueService([Desk, Desk with { Name = "Another desk" }]);

        var output = await HueMcpTools.GetHueStatus(Desk.Id.ToString(), hue);

        Assert.Contains("not unique", output);
        Assert.Empty(hue.StatusRequests);
    }

    [Fact]
    public async Task SetHueBrightness_DiscoveredLightAndValidBrightness_ExecutesBrightnessAction()
    {
        var hue = new FakeHueService([Desk]);

        var output = await HueMcpTools.SetHueBrightness(Desk.Id.ToString(), 45, hue);

        Assert.Contains("Applied SetBrightness", output);
        Assert.Equal(45, Assert.Single(hue.Executed).BrightnessPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SetHueBrightness_BrightnessIsOutsideAllowedRange_DoesNotExecuteAction(int brightness)
    {
        var hue = new FakeHueService([Desk]);

        var output = await HueMcpTools.SetHueBrightness(Desk.Id.ToString(), brightness, hue);

        Assert.Contains("Not applied", output);
        Assert.Empty(hue.Executed);
    }

    [Theory]
    [InlineData("invented-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("c33a29e4-b857-4e38-8ad9-39d41773cb36")]
    public async Task SetHuePower_IdIsMalformedEmptyOrUndiscovered_DoesNotExecuteAction(string resourceId)
    {
        var hue = new FakeHueService([Desk]);

        var output = await HueMcpTools.SetHuePower(resourceId, true, hue);

        Assert.Contains("currently discovered", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task SetHuePower_TargetIsZone_ExecutesPowerAction()
    {
        var zone = new HueResourceGroup(Guid.NewGuid(), "Upstairs", HueResourceType.Zone, []);
        var hue = new FakeHueService([zone]);

        var output = await HueMcpTools.SetHuePower(zone.Id.ToString(), true, hue);

        Assert.Contains("Applied TurnOn", output);
        Assert.Equal(zone.Id, Assert.Single(hue.Executed).Target.Id);
    }

    [Fact]
    public async Task SetHuePower_TargetIsScene_DoesNotExecuteAction()
    {
        var hue = new FakeHueService([Relax]);

        var output = await HueMcpTools.SetHuePower(Relax.Id.ToString(), true, hue);

        Assert.Contains("cannot be used on a scene", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task ActivateHueScene_TargetIsKnownScene_ExecutesSceneActivation()
    {
        var hue = new FakeHueService([Relax]);

        var output = await HueMcpTools.ActivateHueScene(Relax.Id.ToString(), hueService: hue);

        Assert.Contains("Applied ActivateScene", output);
        Assert.Equal(HueOperation.ActivateScene, Assert.Single(hue.Executed).Operation);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task ActivateHueScene_ExplicitRoomIdIsBlank_DoesNotExecuteAction(string roomId)
    {
        var hue = new FakeHueService([Relax, Office]);

        var output = await HueMcpTools.ActivateHueScene(Relax.Id.ToString(), roomId, hue);

        Assert.Contains("Not applied", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task ActivateHueScene_RoomDoesNotMatchSceneAssociation_DoesNotExecuteAction()
    {
        var otherScene = new HueResource(Guid.NewGuid(), "Relax", HueResourceType.Scene);
        var kitchen = new HueResourceGroup(Guid.NewGuid(), "Kitchen", HueResourceType.Room, [otherScene]);
        var hue = new FakeHueService([Relax, otherScene, Office, kitchen]);

        var mismatch = await HueMcpTools.ActivateHueScene(Relax.Id.ToString(), kitchen.Id.ToString(), hue);
        var match = await HueMcpTools.ActivateHueScene(Relax.Id.ToString().ToUpperInvariant(),
            Office.Id.ToString().ToUpperInvariant(), hue);

        Assert.Contains("not associated", mismatch);
        Assert.Contains("Applied ActivateScene", match);
        Assert.Equal(Relax.Id, Assert.Single(hue.Executed).Target.Id);
    }

    [Fact]
    public async Task ActivateHueScene_DuplicateRoomId_DoesNotExecuteEvenIfOnlyOneRoomContainsScene()
    {
        var hue = new FakeHueService([Relax, Office, Office with { Resources = [] }]);

        var output = await HueMcpTools.ActivateHueScene(Relax.Id.ToString(), Office.Id.ToString(), hue);

        Assert.Contains("Not applied", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task ListHueResources_BridgeNetworkFails_ReturnsConnectionFailureExplanation()
    {
        var hue = new FakeHueService([],
            new HttpRequestException("network unavailable", new SocketException((int)SocketError.HostUnreachable)));

        var output = await HueMcpTools.ListHueResources(hueService: hue);

        Assert.Contains("Could not reach the Hue Bridge", output);
        Assert.Contains("HostUnreachable", output);
    }

    private sealed class FakeHueService(
        IReadOnlyList<HueResource> resources,
        Exception? resourceError = null,
        HueStatus? status = null) : IHueService
    {
        public List<HueAction> Executed { get; } = [];
        public List<HueResource> StatusRequests { get; } = [];

        public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default)
        {
            return resourceError is null
                ? Task.FromResult(resources)
                : Task.FromException<IReadOnlyList<HueResource>>(resourceError);
        }

        public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
        {
            Executed.Add(action);
            return Task.CompletedTask;
        }

        public Task<HueStatus> GetStatusAsync(HueResource resource, CancellationToken cancellationToken = default)
        {
            StatusRequests.Add(resource);
            return Task.FromResult(status ?? new HueStatus(resource.Id, resource.Name, resource.Type, false));
        }
    }
}
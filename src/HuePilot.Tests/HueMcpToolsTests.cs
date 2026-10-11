using HuePilot.Application;
using HuePilot.Mcp;
using System.Net.Http;
using System.Net.Sockets;

namespace HuePilot.Tests;

public sealed class HueMcpToolsTests
{
    private static readonly HueResource Desk = new("light-desk", "Desk", "light", [new("room-office", "Office", "room")]);
    private static readonly HueResource Office = new("room-office", "Office", "room");
    private static readonly HueResource Relax = new("scene-relax", "Relax", "scene", [new("room-office", "Office", "room")]);

    [Fact]
    public async Task ListResourcesReturnsNamesTypesAndIds()
    {
        var hue = new FakeHueService([Desk, Office, Relax]);

        var output = await HueMcpTools.ListHueResources(hueService: hue);

        Assert.Contains("Desk", output);
        Assert.Contains("light-desk", output);
        Assert.Contains("Relax", output);
        Assert.Contains("Office", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task BrightnessToolExecutesValidDiscoveredLightAction()
    {
        var hue = new FakeHueService([Desk]);

        var output = await HueMcpTools.SetHueBrightness("light-desk", 45, hue);

        Assert.Contains("Applied SetBrightness", output);
        Assert.Equal(45, Assert.Single(hue.Executed).BrightnessPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task InvalidBrightnessNeverExecutes(int brightness)
    {
        var hue = new FakeHueService([Desk]);

        var output = await HueMcpTools.SetHueBrightness("light-desk", brightness, hue);

        Assert.Contains("Not applied", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task UnknownResourceIdNeverExecutes()
    {
        var hue = new FakeHueService([Desk]);

        var output = await HueMcpTools.SetHuePower("invented-id", true, hue);

        Assert.Contains("currently discovered", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task PowerToolCannotTargetScene()
    {
        var hue = new FakeHueService([Relax]);

        var output = await HueMcpTools.SetHuePower("scene-relax", true, hue);

        Assert.Contains("cannot be used on a scene", output);
        Assert.Empty(hue.Executed);
    }

    [Fact]
    public async Task SceneToolCanOnlyActivateKnownScene()
    {
        var hue = new FakeHueService([Relax]);

        var output = await HueMcpTools.ActivateHueScene("scene-relax", hueService: hue);

        Assert.Contains("Applied ActivateScene", output);
        Assert.Equal(HueOperation.ActivateScene, Assert.Single(hue.Executed).Operation);
    }

    [Fact]
    public async Task SceneToolValidatesRequestedRoomAssociation()
    {
        var hue = new FakeHueService([Relax, new("room-kitchen", "Kitchen", "room")]);

        var mismatch = await HueMcpTools.ActivateHueScene("scene-relax", "room-kitchen", hue);
        var match = await HueMcpTools.ActivateHueScene("scene-relax", "room-office", hue);

        Assert.Contains("not associated", mismatch);
        Assert.Contains("Applied ActivateScene", match);
        Assert.Single(hue.Executed);
    }

    [Fact]
    public async Task ResourceLookupExplainsBridgeNetworkFailure()
    {
        var hue = new FakeHueService([], new HttpRequestException("network unavailable", new SocketException((int)SocketError.HostUnreachable)));

        var output = await HueMcpTools.ListHueResources(hueService: hue);

        Assert.Contains("Could not reach the Hue Bridge", output);
        Assert.Contains("HostUnreachable", output);
    }

    private sealed class FakeHueService(IReadOnlyList<HueResource> resources, Exception? resourceError = null) : IHueService
    {
        public List<HueAction> Executed { get; } = [];
        public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default) => resourceError is null
            ? Task.FromResult(resources)
            : Task.FromException<IReadOnlyList<HueResource>>(resourceError);
        public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
        {
            Executed.Add(action);
            return Task.CompletedTask;
        }
    }
}

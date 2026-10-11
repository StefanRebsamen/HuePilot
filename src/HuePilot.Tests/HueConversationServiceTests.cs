using HuePilot.Application;
using HuePilot.Infrastructure;
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

    private sealed class FakeIntentParser(HueIntent intent) : IIntentParser
    {
        public Task<HueIntent> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
            IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default) => Task.FromResult(intent);
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

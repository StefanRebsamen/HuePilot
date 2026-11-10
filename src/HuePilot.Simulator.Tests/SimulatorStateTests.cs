using HuePilot.Simulator.Simulation;

namespace HuePilot.Simulator.Tests;

public sealed class SimulatorStateTests
{
    [Fact]
    public void CreateDefault_ExpandedCatalogHasGardenAndOverlappingZones()
    {
        var snapshot = SimulatorState.CreateDefault().GetSnapshot();

        Assert.Equal(6, snapshot.Rooms.Count);
        Assert.Equal(11, snapshot.Lights.Count);
        Assert.Equal(3, snapshot.Zones.Count);

        var garden = Assert.Single(snapshot.Rooms, room => room.Name == "Garden");
        Assert.Equal(5, garden.LightIds.Count);

        var lightIds = snapshot.Lights.Select(light => light.Id).ToHashSet();
        Assert.All(snapshot.Rooms.Concat(snapshot.Zones), group =>
            Assert.All(group.LightIds, lightId => Assert.Contains(lightId, lightIds)));
    }

    [Fact]
    public void UpdateGroupedLight_ValidBrightness_UpdatesEveryOwnedLightAndDerivedGroupState()
    {
        var state = SimulatorState.CreateDefault();
        var zone = Assert.Single(state.GetSnapshot().Zones, zone => zone.Name == "Downstairs");

        state.UpdateGroupedLight(zone.GroupedLightId,
            new GroupedLightUpdateRequest(On: true, Brightness: 65, Color: null));

        var snapshot = state.GetSnapshot();
        Assert.All(snapshot.Lights.Where(light => zone.LightIds.Contains(light.Id)),
            light => Assert.Equal(65, light.Brightness));
        Assert.Equal(65, snapshot.GroupedLights.Single(group => group.Id == zone.GroupedLightId).Brightness);
    }

    [Fact]
    public void ActivateScene_AppliesItsPresetToAssociatedLights()
    {
        var state = SimulatorState.CreateDefault();
        var scene = state.GetSnapshot().Scenes.Single();

        state.ActivateScene(scene.Id);

        var snapshot = state.GetSnapshot();
        Assert.All(snapshot.Lights.Where(light => scene.LightIds.Contains(light.Id)), light => Assert.True(light.IsOn));
    }

    [Fact]
    public void UpdateLight_InvalidBrightness_LeavesStateUnchanged()
    {
        var state = SimulatorState.CreateDefault();
        var light = state.GetSnapshot().Lights.First();

        Assert.False(state.UpdateLight(light.Id, new LightUpdateRequest(On: null, Brightness: 101, Color: null)));

        Assert.Equal(light, state.GetSnapshot().Lights.Single(current => current.Id == light.Id));
    }

    [Fact]
    public void GetSnapshot_ZoneGroupedLight_IdentifiesZoneOwner()
    {
        var snapshot = SimulatorState.CreateDefault().GetSnapshot();
        var zone = Assert.Single(snapshot.Zones, zone => zone.Name == "Downstairs");

        var groupedLight = snapshot.GroupedLights.Single(group => group.Id == zone.GroupedLightId);

        Assert.Equal("zone", groupedLight.OwnerType);
    }

    [Fact]
    public async Task GetSnapshot_ConcurrentMutations_ReturnsCompleteSnapshots()
    {
        var state = SimulatorState.CreateDefault();
        var light = state.GetSnapshot().Lights.First();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(index => Task.Run(() =>
        {
            state.UpdateLight(light.Id, new LightUpdateRequest(index % 2 == 0, 40 + index % 50, null));
            var snapshot = state.GetSnapshot();
            Assert.NotEmpty(snapshot.Lights);
            Assert.All(snapshot.Rooms, room => Assert.NotEmpty(room.LightIds));
        })));
    }
}

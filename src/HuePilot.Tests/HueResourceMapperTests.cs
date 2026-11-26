using HueApi.Models;
using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;
using ApiLight = HueApi.Models.Light;
using ApiRoom = HueApi.Models.Room;
using ApiScene = HueApi.Models.Scene;
using ApiZone = HueApi.Models.Zone;

namespace HuePilot.Tests;

public sealed class HueResourceMapperTests
{
    [Fact]
    public void Map_DuplicateLightIds_PreservesBothCatalogEntriesAndTheirMembership()
    {
        var lightId = Guid.NewGuid();
        var resources = HueResourceMapper.Map(
            [
                new ApiLight { Id = lightId, Metadata = new Metadata { Name = "Desk" } },
                new ApiLight { Id = lightId, Metadata = new Metadata { Name = "Ceiling" } }
            ],
            [
                new ApiRoom
                    {
                        Id = Guid.NewGuid(), Children = [new ResourceIdentifier { Rid = lightId, Rtype = "light" }]
                    }
            ],
            [], [], []);

        var duplicateLights = resources.Where(resource => resource.Id == lightId).ToArray();
        Assert.Equal(["Desk", "Ceiling"], duplicateLights.Select(light => light.Name));
        var room = Assert.Single(resources.OfType<HueResourceGroup>());
        Assert.Collection(room.Resources, light => Assert.Same(duplicateLights[0], light),
            light => Assert.Same(duplicateLights[1], light));
    }

    [Fact]
    public void Map_GroupChildren_ContainCanonicalLightsByServiceId()
    {
        var bedroomId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var lightId = Guid.NewGuid();
        var otherLightId = Guid.NewGuid();

        var resources = HueResourceMapper.Map(
            [
                new ApiLight { Id = lightId, Metadata = new Metadata { Name = "Bedside" } },
                new ApiLight { Id = otherLightId, Metadata = new Metadata { Name = "Bedside" } }
            ],
            [
                new ApiRoom
                {
                    Id = bedroomId,
                    Metadata = new Metadata { Name = "Bedroom" },
                    Children =
                    [
                        new ResourceIdentifier { Rid = deviceId, Rtype = "device" },
                        new ResourceIdentifier { Rid = Guid.NewGuid(), Rtype = "device" }
                    ]
                }
            ],
            [
                new ApiZone
                {
                    Id = zoneId,
                    Metadata = new Metadata { Name = "Upstairs" },
                    Children =
                    [
                        new ResourceIdentifier { Rid = deviceId, Rtype = "device" },
                        new ResourceIdentifier { Rid = otherLightId, Rtype = "light" },
                        new ResourceIdentifier { Rid = Guid.NewGuid(), Rtype = "light" }
                    ]
                }
            ], [],
            [
                new Device
                {
                    Id = deviceId,
                    Services =
                    [
                        new ResourceIdentifier { Rid = lightId, Rtype = "light" },
                        new ResourceIdentifier { Rid = Guid.NewGuid(), Rtype = "light" },
                        new ResourceIdentifier { Rid = otherLightId, Rtype = "button" }
                    ]
                }
            ]);

        var room = Assert.Single(resources.OfType<HueResourceGroup>(), group => group.Id == bedroomId);
        var zone = Assert.Single(resources.OfType<HueResourceGroup>(), group => group.Id == zoneId);
        var light = Assert.Single(resources, resource => resource.Id == lightId);
        var otherLight = Assert.Single(resources, resource => resource.Id == otherLightId);

        Assert.Equal(("Bedroom", HueResourceType.Room), (room.Name, room.Type));
        Assert.Equal(("Upstairs", HueResourceType.Zone), (zone.Name, zone.Type));
        Assert.Same(light, Assert.Single(room.Resources));
        Assert.Equal(2, zone.Resources.Count);
        Assert.Same(light, Assert.Single(zone.Resources, resource => resource.Id == lightId));
        Assert.Same(otherLight, Assert.Single(zone.Resources, resource => resource.Id == otherLightId));
    }

    [Fact]
    public void Map_Scenes_AppearOnlyInTheirExactOwningGroup()
    {
        var roomId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var roomSceneId = Guid.NewGuid();
        var zoneSceneId = Guid.NewGuid();
        var unknownOwnerSceneId = Guid.NewGuid();
        var mismatchedOwnerTypeSceneId = Guid.NewGuid();

        var resources = HueResourceMapper.Map([],
            [new ApiRoom { Id = roomId, Metadata = new Metadata { Name = "Bedroom" } }],
            [new ApiZone { Id = zoneId, Metadata = new Metadata { Name = "Bedroom" } }],
            [
                new ApiScene
                {
                    Id = roomSceneId,
                    Metadata = new Metadata { Name = "Relax" },
                    Group = new ResourceIdentifier { Rid = roomId, Rtype = "room" }
                },
                new ApiScene
                {
                    Id = zoneSceneId,
                    Metadata = new Metadata { Name = "Relax" },
                    Group = new ResourceIdentifier { Rid = zoneId, Rtype = "zone" }
                },
                new ApiScene
                {
                    Id = unknownOwnerSceneId,
                    Metadata = new Metadata { Name = "Relax" },
                    Group = new ResourceIdentifier { Rid = Guid.NewGuid(), Rtype = "room" }
                },
                new ApiScene
                {
                    Id = mismatchedOwnerTypeSceneId,
                    Metadata = new Metadata { Name = "Relax" },
                    Group = new ResourceIdentifier { Rid = roomId, Rtype = "zone" }
                }
            ], []);

        var room = Assert.Single(resources.OfType<HueResourceGroup>(), group => group.Id == roomId);
        var zone = Assert.Single(resources.OfType<HueResourceGroup>(), group => group.Id == zoneId);
        var roomScene = Assert.Single(resources, resource => resource.Id == roomSceneId);
        var zoneScene = Assert.Single(resources, resource => resource.Id == zoneSceneId);

        Assert.Same(roomScene, Assert.Single(room.Resources));
        Assert.Same(zoneScene, Assert.Single(zone.Resources));
        Assert.Contains(resources, resource => resource.Id == unknownOwnerSceneId);
        Assert.Contains(resources, resource => resource.Id == mismatchedOwnerTypeSceneId);
    }
}
using HueApi.Models;
using HuePilot.Infrastructure;
using ApiLight = HueApi.Models.Light;
using ApiRoom = HueApi.Models.Room;
using ApiScene = HueApi.Models.Scene;
using ApiZone = HueApi.Models.Zone;

namespace HuePilot.Tests;

public sealed class HueResourceMapperTests
{
    [Fact]
    public void MapsDeviceMembershipAndSceneGroupToNamedRoom()
    {
        var bedroomId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var lightId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();

        var resources = HueResourceMapper.Map(
            [new ApiLight { Id = lightId, Metadata = new Metadata { Name = "Bedside" } }],
            [new ApiRoom
            {
                Id = bedroomId,
                Metadata = new Metadata { Name = "Bedroom" },
                Children = [new ResourceIdentifier { Rid = deviceId, Rtype = "device" }]
            }],
            [new ApiZone
            {
                Id = zoneId,
                Metadata = new Metadata { Name = "Upstairs" },
                Children = [new ResourceIdentifier { Rid = deviceId, Rtype = "device" }]
            }],
            [new ApiScene
            {
                Id = sceneId,
                Metadata = new Metadata { Name = "Relax" },
                Group = new ResourceIdentifier { Rid = bedroomId, Rtype = "room" }
            }],
            [new Device
            {
                Id = deviceId,
                Services = [new ResourceIdentifier { Rid = lightId, Rtype = "light" }]
            }]);

        var light = Assert.Single(resources, resource => resource.Id == lightId.ToString());
        var scene = Assert.Single(resources, resource => resource.Id == sceneId.ToString());

        Assert.Contains(light.Groups!, group => group.Name == "Bedroom" && group.Type == "room");
        Assert.Contains(light.Groups!, group => group.Name == "Upstairs" && group.Type == "zone");
        Assert.Contains(scene.Groups!, group => group.Id == bedroomId.ToString() && group.Name == "Bedroom");
    }
}

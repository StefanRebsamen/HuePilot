using HueApi.Models;
using HuePilot.Infrastructure;
using ApiGroupedLight = HueApi.Models.GroupedLight;
using ApiRoom = HueApi.Models.Room;

namespace HuePilot.Tests;

public sealed class HueRoomControlResolverTests
{
    [Fact]
    public void FindsGroupedLightByItsRoomOwnerWhenRoomHasNoGroupedServices()
    {
        var roomId = Guid.NewGuid();
        var groupedLightId = Guid.NewGuid();
        var room = new ApiRoom { Id = roomId, GroupedServices = [] };
        var groupedLights = new[]
        {
            new ApiGroupedLight { Id = Guid.NewGuid(), Owner = new ResourceIdentifier { Rid = Guid.NewGuid(), Rtype = "room" } },
            new ApiGroupedLight { Id = groupedLightId, Owner = new ResourceIdentifier { Rid = roomId, Rtype = "room" } }
        };

        Assert.Equal(groupedLightId, HueRoomControlResolver.FindGroupedLightId(room, groupedLights));
    }

    [Fact]
    public void DoesNotGuessWhenGroupedLightOwnershipIsMissingOrAmbiguous()
    {
        var roomId = Guid.NewGuid();
        var room = new ApiRoom { Id = roomId, GroupedServices = [] };
        var unrelated = new ApiGroupedLight { Id = Guid.NewGuid(), Owner = new ResourceIdentifier { Rid = Guid.NewGuid(), Rtype = "room" } };

        Assert.Null(HueRoomControlResolver.FindGroupedLightId(room, [unrelated]));
        Assert.Null(HueRoomControlResolver.FindGroupedLightId(room, [
            new ApiGroupedLight { Id = Guid.NewGuid(), Owner = new ResourceIdentifier { Rid = roomId, Rtype = "room" } },
            new ApiGroupedLight { Id = Guid.NewGuid(), Owner = new ResourceIdentifier { Rid = roomId, Rtype = "room" } }
        ]));
    }
}

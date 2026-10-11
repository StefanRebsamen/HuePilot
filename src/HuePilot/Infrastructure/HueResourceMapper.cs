using HueApi.Models;
using HuePilot.Application;
using ApiDevice = HueApi.Models.Device;
using ApiLight = HueApi.Models.Light;
using ApiRoom = HueApi.Models.Room;
using ApiScene = HueApi.Models.Scene;
using ApiZone = HueApi.Models.Zone;
using AppHueResource = HuePilot.Application.HueResource;

namespace HuePilot.Infrastructure;

/// <summary>Maps Bridge v2 group membership into application resources.</summary>
public static class HueResourceMapper
{
    /// <summary>
    /// Joins room/zone children to light services on their devices and attaches each scene to its owner group.
    /// </summary>
    /// <param name="lights">Light service resources returned by the Bridge.</param>
    /// <param name="rooms">Room groups and their child devices.</param>
    /// <param name="zones">Zone groups and their child devices.</param>
    /// <param name="scenes">Scenes and their owning room or zone.</param>
    /// <param name="devices">Devices that connect group child IDs to light service IDs.</param>
    /// <returns>Application resources carrying display names, stable IDs, and associations.</returns>
    public static IReadOnlyList<AppHueResource> Map(
        IEnumerable<ApiLight> lights,
        IEnumerable<ApiRoom> rooms,
        IEnumerable<ApiZone> zones,
        IEnumerable<ApiScene> scenes,
        IEnumerable<ApiDevice> devices)
    {
        var roomList = rooms.ToArray();
        var zoneList = zones.ToArray();
        var deviceById = devices.ToDictionary(device => device.Id);
        var groupsById = roomList.Select(room => new HueResourceGroup(room.Id.ToString(), Name(room.Metadata?.Name, "Unnamed room"), "room"))
            .Concat(zoneList.Select(zone => new HueResourceGroup(zone.Id.ToString(), Name(zone.Metadata?.Name, "Unnamed zone"), "zone")))
            .ToDictionary(group => Guid.Parse(group.Id));

        var lightGroups = new Dictionary<Guid, List<HueResourceGroup>>();
        AddMembership(roomList.Select(room => (room.Id, room.Children)), "room");
        AddMembership(zoneList.Select(zone => (zone.Id, zone.Children)), "zone");

        // A scene's group is its activation target; retaining it lets the UI and AI distinguish
        // same-named scenes that control different rooms or zones.
        var sceneGroups = scenes.ToDictionary(scene => scene.Id, scene =>
            scene.Group is not null && groupsById.TryGetValue(scene.Group.Rid, out var group)
                ? (IReadOnlyList<HueResourceGroup>)[group]
                : []);

        return lights.Select(light => new AppHueResource(
                light.Id.ToString(),
                Name(light.Metadata?.Name, "Unnamed light"),
                "light",
                lightGroups.TryGetValue(light.Id, out var memberships) ? memberships : []))
            .Concat(roomList.Select(room => new AppHueResource(
                room.Id.ToString(), Name(room.Metadata?.Name, "Unnamed room"), "room")))
            .Concat(scenes.Select(scene => new AppHueResource(
                scene.Id.ToString(),
                Name(scene.Metadata?.Name, "Unnamed scene"),
                "scene",
                sceneGroups[scene.Id])))
            .ToArray();

        void AddMembership(IEnumerable<(Guid Id, List<ResourceIdentifier> Children)> groupChildren, string groupType)
        {
            foreach (var (groupId, children) in groupChildren)
            {
                if (!groupsById.TryGetValue(groupId, out var group) || group.Type != groupType)
                    continue;

                foreach (var child in children)
                {
                    if (child.Rtype == "light")
                    {
                        AddLightGroup(child.Rid, group);
                    }
                    else if (child.Rtype == "device" && deviceById.TryGetValue(child.Rid, out var device))
                    {
                        // Rooms commonly list devices rather than light services. Follow the
                        // device's service links to attach the group to the controllable light.
                        foreach (var service in (device.Services ?? []).Where(service => service.Rtype == "light"))
                            AddLightGroup(service.Rid, group);
                    }
                }
            }
        }

        void AddLightGroup(Guid lightId, HueResourceGroup group)
        {
            if (!lightGroups.TryGetValue(lightId, out var memberships))
                lightGroups[lightId] = memberships = [];
            if (memberships.All(existing => existing.Id != group.Id))
                memberships.Add(group);
        }
    }

    private static string Name(string? name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name;
}

using HueApi.Models;
using HuePilot.Application.Hue.Models;
using ApiDevice = HueApi.Models.Device;
using ApiLight = HueApi.Models.Light;
using ApiRoom = HueApi.Models.Room;
using ApiScene = HueApi.Models.Scene;
using ApiZone = HueApi.Models.Zone;
using AppHueResource = HuePilot.Application.Hue.Models.HueResource;

namespace HuePilot.Application.Hue;

/// <summary>
/// Maps Bridge v2 group membership into application resources.
/// </summary>
public static class HueResourceMapper
{
    /// <summary>
    /// Maps room and zone children to their light services and adds each scene to its owning group.
    /// </summary>
    /// <param name="lights">Light service resources returned by the Bridge.</param>
    /// <param name="rooms">Room groups and their children.</param>
    /// <param name="zones">Zone groups and their children.</param>
    /// <param name="scenes">Scenes and their owning room or zone.</param>
    /// <param name="devices">
    /// Devices that connect group child IDs to light service IDs.
    /// </param>
    /// <returns>
    /// A flat catalog of application resources, including rooms and zones with their members.
    /// </returns>
    public static IReadOnlyList<AppHueResource> Map(IEnumerable<ApiLight> lights, IEnumerable<ApiRoom> rooms,
        IEnumerable<ApiZone> zones, IReadOnlyCollection<ApiScene> scenes, IEnumerable<ApiDevice> devices)
    {
        var deviceById = devices.ToDictionary(device => device.Id);
        var lightResources = lights.Select(light => new AppHueResource(light.Id,
            Name(light.Metadata?.Name, "Unnamed light"), HueResourceType.Light)).ToArray();
        var lightsById = lightResources.ToLookup(light => light.Id);
        var sceneResources = scenes.Select(scene => (scene.Group,
                Resource: new AppHueResource(scene.Id, Name(scene.Metadata?.Name, "Unnamed scene"),
                    HueResourceType.Scene)))
            .ToArray();
        var scenesByGroup = sceneResources.Where(scene => scene.Group is not null)
            .ToLookup(scene => (scene.Group!.Rid, scene.Group.Rtype), scene => scene.Resource);

        return
        [
            .. lightResources,
            .. rooms.Select(room => MapGroup(room.Id, Name(room.Metadata?.Name, "Unnamed room"),
                HueResourceType.Room, room.Children)),
            .. zones.Select(zone => MapGroup(zone.Id, Name(zone.Metadata?.Name, "Unnamed zone"),
                HueResourceType.Zone, zone.Children)),
            .. sceneResources.Select(scene => scene.Resource)
        ];

        HueResourceGroup MapGroup(Guid id, string name, HueResourceType type, IEnumerable<ResourceIdentifier>? children)
        {
            var memberLights = (children ?? []).SelectMany(GetLightIds).Distinct()
                .SelectMany(lightId => lightsById[lightId]);
            var memberScenes = scenesByGroup[(id, type.ToHueV2WireType())];

            return new HueResourceGroup(id, name, type, [.. memberLights, .. memberScenes]);
        }

        IEnumerable<Guid> GetLightIds(ResourceIdentifier child)
        {
            if (child.Rtype == HueResourceType.Light.ToHueV2WireType())
            {
                return [child.Rid];
            }

            if (child.Rtype == HueResourceType.Device.ToHueV2WireType() &&
                deviceById.TryGetValue(child.Rid, out var device))
            {
                return (device.Services ?? [])
                    .Where(service => service.Rtype == HueResourceType.Light.ToHueV2WireType())
                    .Select(service => service.Rid);
            }

            return [];
        }
    }

    private static string Name(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
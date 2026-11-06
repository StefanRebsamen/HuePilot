using HueApi;
using HueApi.Models;
using HueApi.Models.Requests;
using HuePilot.Application.Hue;
using AppHueResource = HuePilot.Application.Hue.HueResource;

namespace HuePilot.Infrastructure.Hue;

/// <summary>Production Hue service adapter backed by the local Hue Bridge v2 API.</summary>
/// <param name="configuration">Configuration containing Bridge address and application key secrets.</param>
public sealed class HueApiHueService(IConfiguration configuration) : IHueService
{
    private readonly string _applicationKey = configuration["Bridge:ApplicationKey"] ?? "";
    private readonly string _bridgeIp = configuration["Bridge:IpAddress"] ?? "";

    /// <summary>Indicates whether both required Bridge settings are present.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_bridgeIp) && !string.IsNullOrWhiteSpace(_applicationKey);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppHueResource>> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        var api = CreateApi();
        var lights = await api.Light.GetAllAsync();
        var rooms = await api.Room.GetAllAsync();
        var zones = await api.Zone.GetAllAsync();
        var scenes = await api.Scene.GetAllAsync();
        // Room/zone membership points through devices, so the mapper also needs device service links.
        var devices = await api.Device.GetAllAsync();

        return HueResourceMapper.Map(lights.Data, rooms.Data, zones.Data, scenes.Data, devices.Data);
    }

    /// <inheritdoc />
    public async Task<string> GetStatusAsync(AppHueResource resource, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(resource.Id, out var resourceId))
        {
            throw new InvalidOperationException("The Hue resource identifier is invalid.");
        }

        var api = CreateApi();
        if (resource.Type.Equals(HueResourceTypes.Light, StringComparison.OrdinalIgnoreCase))
        {
            var light = (await api.Light.GetAllAsync()).Data.Single(x => x.Id == resourceId);
            var brightness = light.Dimming?.Brightness;
            return light.On?.IsOn == true
                ? $"{resource.Name} is on" +
                  (brightness is null ? "." : $" at {Math.Round(brightness.Value)}% brightness.")
                : $"{resource.Name} is off.";
        }

        if (resource.Type.Equals(HueResourceTypes.Room, StringComparison.OrdinalIgnoreCase) ||
            resource.Type.Equals(HueResourceTypes.Zone, StringComparison.OrdinalIgnoreCase))
        {
            var groupedLightId = await FindGroupedLightIdAsync(api, resourceId, resource.Type, cancellationToken);
            if (groupedLightId is null)
            {
                throw new InvalidOperationException(
                    $"Hue Bridge did not return a grouped light for {resource.Type} ‘{resource.Name}’.");
            }

            var groupedLight = (await api.GroupedLight.GetAllAsync()).Data.Single(x => x.Id == groupedLightId.Value);
            var brightness = groupedLight.Dimming?.Brightness;
            return groupedLight.On?.IsOn == true
                ? $"{resource.Name} is on" +
                  (brightness is null ? "." : $" at {Math.Round(brightness.Value)}% brightness.")
                : $"{resource.Name} is off.";
        }

        throw new InvalidOperationException("Hue status can only be reported for lights, rooms, and zones.");
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
    {
        var api = CreateApi();
        var resources = await GetResourcesAsync(cancellationToken);
        var current = resources.SingleOrDefault(x => x.Id == action.Target.Id &&
                                                     x.Type.Equals(action.Target.Type,
                                                         StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            throw new InvalidOperationException("That Hue resource is no longer available. Please try again.");
        }

        if (!Guid.TryParse(current.Id, out var resourceId))
        {
            throw new InvalidOperationException("The Hue resource identifier is invalid.");
        }

        switch (action.Operation)
        {
            case HueOperation.ActivateScene:
                if (current.Type != HueResourceTypes.Scene)
                {
                    throw new InvalidOperationException("Only an existing scene can be activated.");
                }

                await api.Scene.UpdateAsync(resourceId,
                    new UpdateScene { Recall = new Recall { Action = SceneRecallAction.active } });
                break;
            case HueOperation.TurnOn:
            case HueOperation.TurnOff:
            case HueOperation.SetBrightness:
            case HueOperation.SetColor:
                if (current.Type == HueResourceTypes.Light)
                {
                    await api.Light.UpdateAsync(resourceId, BuildLightUpdate(action));
                    break;
                }

                if (current.Type == HueResourceTypes.Room || current.Type == HueResourceTypes.Zone)
                {
                    var groupedLightId = await FindGroupedLightIdAsync(
                        api, resourceId, current.Type, cancellationToken);

                    if (groupedLightId is null)
                    {
                        throw new InvalidOperationException(
                            $"Hue Bridge did not return a grouped light owned by {current.Type} ‘{current.Name}’.");
                    }

                    await api.GroupedLight.UpdateAsync(groupedLightId.Value, BuildGroupedUpdate(action));
                    break;
                }

                throw new InvalidOperationException("That action cannot be applied to this Hue resource.");
            default:
                throw new InvalidOperationException("That Hue operation is not allowed.");
        }
    }

    private LocalHueApi CreateApi()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Hue Bridge is not configured. Visit /setup to pair a Bridge and add its settings as user secrets.");
        }

        return new LocalHueApi(_bridgeIp, _applicationKey);
    }

    private static async Task<Guid?> FindGroupedLightIdAsync(LocalHueApi api, Guid groupId, string groupType,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResourceIdentifier>? groupedServices;
        if (groupType.Equals(HueResourceTypes.Room, StringComparison.OrdinalIgnoreCase))
        {
            var room = (await api.Room.GetAllAsync()).Data.Single(x => x.Id == groupId);
            groupedServices = room.GroupedServices;

            var configuredId = groupedServices
                .FirstOrDefault(x => x.Rtype.Equals(HueResourceTypes.GroupedLight,
                    StringComparison.OrdinalIgnoreCase))?.Rid;
            if (configuredId is not null)
            {
                return configuredId;
            }
        }
        else
        {
            var zone = (await api.Zone.GetAllAsync()).Data.Single(x => x.Id == groupId);
            groupedServices = null;
        }

        var groupedLights = await api.GroupedLight.GetAllAsync();
        return HueRoomControlResolver.FindGroupedLightId(groupId, groupType, groupedLights.Data);
    }

    private static UpdateLight BuildLightUpdate(HueAction action)
    {
        var update = new UpdateLight();
        return action.Operation switch
        {
            HueOperation.TurnOn => update.TurnOn(),
            HueOperation.TurnOff => update.TurnOff(),
            HueOperation.SetBrightness when action.BrightnessPercent is >= 1 and <= 100 => update.SetBrightness(
                action.BrightnessPercent.Value),
            HueOperation.SetColor when TryGetColor(action.Color, out var xy) => update.SetColor(xy.X, xy.Y),
            _ => throw new InvalidOperationException("The requested light change is invalid.")
        };
    }

    private static UpdateGroupedLight BuildGroupedUpdate(HueAction action)
    {
        var update = new UpdateGroupedLight();
        return action.Operation switch
        {
            HueOperation.TurnOn => update.TurnOn(),
            HueOperation.TurnOff => update.TurnOff(),
            HueOperation.SetBrightness when action.BrightnessPercent is >= 1 and <= 100 => update.SetBrightness(
                action.BrightnessPercent.Value),
            HueOperation.SetColor when TryGetColor(action.Color, out var xy) => update.SetColor(xy.X, xy.Y),
            _ => throw new InvalidOperationException("The requested room change is invalid.")
        };
    }

    private static bool TryGetColor(string? color, out (double X, double Y) xy)
    {
        xy = color?.Trim().ToLowerInvariant() switch
        {
            "red" => (.675, .322),
            "orange" => (.550, .410),
            "yellow" => (.443, .515),
            "green" => (.214, .709),
            "cyan" => (.170, .340),
            "blue" => (.150, .060),
            "purple" => (.382, .160),
            "pink" => (.458, .181),
            "white" => (.323, .329),
            "warm white" => (.458, .410),
            "cool white" => (.313, .323),
            _ => default
        };
        return xy != default;
    }
}

using HueApi;
using HueApi.Models.Requests;
using HuePilot.Application;
using AppHueResource = HuePilot.Application.HueResource;

namespace HuePilot.Infrastructure;

/// <summary>Production Hue service adapter backed by the local Hue Bridge v2 API.</summary>
/// <param name="configuration">Configuration containing Bridge address and application key secrets.</param>
public sealed class HueApiHueService(IConfiguration configuration) : IHueService
{
    private readonly string _bridgeIp = configuration["Bridge:IpAddress"] ?? "";
    private readonly string _applicationKey = configuration["Bridge:ApplicationKey"] ?? "";

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
    public async Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
    {
        var api = CreateApi();
        var resources = await GetResourcesAsync(cancellationToken);
        var current = resources.SingleOrDefault(x => x.Id == action.Target.Id &&
            x.Type.Equals(action.Target.Type, StringComparison.OrdinalIgnoreCase));
        if (current is null)
            throw new InvalidOperationException("That Hue resource is no longer available. Please try again.");

        if (!Guid.TryParse(current.Id, out var resourceId))
            throw new InvalidOperationException("The Hue resource identifier is invalid.");

        switch (action.Operation)
        {
            case HueOperation.ActivateScene:
                if (current.Type != "scene") throw new InvalidOperationException("Only an existing scene can be activated.");
                await api.Scene.UpdateAsync(resourceId, new UpdateScene
                {
                    Recall = new Recall { Action = SceneRecallAction.active }
                });
                break;
            case HueOperation.TurnOn:
            case HueOperation.TurnOff:
            case HueOperation.SetBrightness:
            case HueOperation.SetColor:
                if (current.Type == "light")
                {
                    await api.Light.UpdateAsync(resourceId, BuildLightUpdate(action));
                    break;
                }

                if (current.Type == "room")
                {
                    var room = (await api.Room.GetAllAsync()).Data.Single(x => x.Id == resourceId);
                    var groupedLightId = room.GroupedServices?
                        .FirstOrDefault(x => x.Rtype.Equals("grouped_light", StringComparison.OrdinalIgnoreCase))?.Rid;
                    if (groupedLightId is null)
                    {
                        // Fall back to the grouped-light owner relationship for Bridges that do
                        // not populate Room.GroupedServices.
                        var groupedLights = await api.GroupedLight.GetAllAsync();
                        groupedLightId = HueRoomControlResolver.FindGroupedLightId(room, groupedLights.Data);
                    }

                    if (groupedLightId is null)
                        throw new InvalidOperationException($"Hue Bridge did not return a grouped light owned by room ‘{room.Metadata?.Name ?? room.Id.ToString()}’.");

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
            throw new InvalidOperationException("Hue Bridge is not configured. Visit /setup to pair a Bridge and add its settings as user secrets.");
        return new LocalHueApi(_bridgeIp, _applicationKey);
    }

    private static UpdateLight BuildLightUpdate(HueAction action)
    {
        var update = new UpdateLight();
        return action.Operation switch
        {
            HueOperation.TurnOn => update.TurnOn(),
            HueOperation.TurnOff => update.TurnOff(),
            HueOperation.SetBrightness when action.BrightnessPercent is >= 1 and <= 100 => update.SetBrightness(action.BrightnessPercent.Value),
            HueOperation.SetColor when TryGetColor(action.Color, out var rgb) => update.SetColor(rgb),
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
            HueOperation.SetBrightness when action.BrightnessPercent is >= 1 and <= 100 => update.SetBrightness(action.BrightnessPercent.Value),
            HueOperation.SetColor when TryGetColor(action.Color, out var rgb) => update.SetColor(rgb),
            _ => throw new InvalidOperationException("The requested room change is invalid.")
        };
    }

    private static bool TryGetColor(string? color, out int rgb)
    {
        var hex = color?.Trim().ToLowerInvariant() switch
        {
            "red" => "FF0000", "orange" => "FF8000", "yellow" => "FFFF00", "green" => "00FF00",
            "cyan" => "00FFFF", "blue" => "0000FF", "purple" => "8000FF", "pink" => "FF69B4",
            "white" => "FFFFFF", "warm white" => "FFD6A1", "cool white" => "D6E8FF", _ => null
        };
        return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out rgb);
    }
}

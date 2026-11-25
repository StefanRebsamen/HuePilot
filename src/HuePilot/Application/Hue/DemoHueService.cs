using HuePilot.Application.Hue.Models;

namespace HuePilot.Application.Hue;

/// <summary>An in-memory stand-in used when Hue Bridge credentials are not configured.</summary>
public sealed class DemoHueService : IHueService
{
    private readonly IReadOnlyList<HueResource> _resources =
    [
        new("room-office", "Office", HueResourceType.Room),
        new("zone-upstairs", "Upstairs", HueResourceType.Zone),
        new("light-desk", "Desk lamp", HueResourceType.Light,
            [new HueResourceGroup("room-office", "Office", HueResourceType.Room)]),
        new("light-ceiling", "Ceiling light", HueResourceType.Light,
            [new HueResourceGroup("room-office", "Office", HueResourceType.Room)]),
        new("scene-focus", "Focus", HueResourceType.Scene,
            [new HueResourceGroup("room-office", "Office", HueResourceType.Room)]),
        new("scene-relax", "Relax", HueResourceType.Scene,
            [new HueResourceGroup("room-office", "Office", HueResourceType.Room)])
    ];

    /// <inheritdoc />
    public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_resources);
    }

    /// <summary>Accepts the request without changing any physical device.</summary>
    /// <param name="action">The action that would have been applied to Hue.</param>
    /// <param name="cancellationToken">Unused in demo mode.</param>
    public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
    {
        // Deliberately does not contact real devices. The UI labels this as demo mode.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<HueStatus> GetStatusAsync(HueResource resource, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HueStatus(resource.Id, resource.Name, resource.Type, true, 40, 0.3127, 0.3290));
    }
}

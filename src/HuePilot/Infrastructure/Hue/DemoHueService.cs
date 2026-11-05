using HuePilot.Application.Hue;

namespace HuePilot.Infrastructure.Hue;

/// <summary>An in-memory stand-in used when Hue Bridge credentials are not configured.</summary>
public sealed class DemoHueService : IHueService
{
    private readonly IReadOnlyList<HueResource> _resources =
    [
        new("room-office", "Office", HueResourceTypes.Room), new("zone-upstairs", "Upstairs", HueResourceTypes.Zone),
        new("light-desk", "Desk lamp", HueResourceTypes.Light,
            [new HueResourceGroup("room-office", "Office", HueResourceTypes.Room)]),
        new("light-ceiling", "Ceiling light", HueResourceTypes.Light,
            [new HueResourceGroup("room-office", "Office", HueResourceTypes.Room)]),
        new("scene-focus", "Focus", HueResourceTypes.Scene,
            [new HueResourceGroup("room-office", "Office", HueResourceTypes.Room)]),
        new("scene-relax", "Relax", HueResourceTypes.Scene,
            [new HueResourceGroup("room-office", "Office", HueResourceTypes.Room)])
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
    public Task<string> GetStatusAsync(HueResource resource, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"{resource.Name} is on at 40% brightness in demo mode.");
    }
}

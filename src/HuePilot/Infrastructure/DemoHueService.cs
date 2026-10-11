using HuePilot.Application;

namespace HuePilot.Infrastructure;

/// <summary>An in-memory stand-in used when Hue Bridge credentials are not configured.</summary>
public sealed class DemoHueService : IHueService
{
    private readonly IReadOnlyList<HueResource> _resources =
    [
        new("room-office", "Office", "room"),
        new("light-desk", "Desk lamp", "light", [new("room-office", "Office", "room")]),
        new("light-ceiling", "Ceiling light", "light", [new("room-office", "Office", "room")]),
        new("scene-focus", "Focus", "scene", [new("room-office", "Office", "room")]),
        new("scene-relax", "Relax", "scene", [new("room-office", "Office", "room")])
    ];

    /// <inheritdoc />
    public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(_resources);

    /// <summary>Accepts the request without changing any physical device.</summary>
    /// <param name="action">The action that would have been applied to Hue.</param>
    /// <param name="cancellationToken">Unused in demo mode.</param>
    public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
    {
        // Deliberately does not contact real devices. The UI labels this as demo mode.
        return Task.CompletedTask;
    }
}

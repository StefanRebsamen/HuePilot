using HuePilot.Application.Hue.Models;

namespace HuePilot.Application.Hue;

/// <summary>An in-memory stand-in used when Hue Bridge credentials are not configured.</summary>
public sealed class DemoHueService : IHueService
{
    private readonly IReadOnlyList<HueResource> _resources;

    /// <summary>Creates the catalog of sample rooms, zones, lights, and scenes.</summary>
    public DemoHueService()
    {
        var desk = new HueResource(new Guid("cb84c10e-d817-4ed8-a66c-000000000001"), "Desk lamp",
            HueResourceType.Light);
        var ceiling = new HueResource(new Guid("cb84c10e-d817-4ed8-a66c-000000000002"), "Ceiling light",
            HueResourceType.Light);
        var focus = new HueResource(new Guid("cb84c10e-d817-4ed8-a66c-000000000003"), "Focus", HueResourceType.Scene);
        var relax = new HueResource(new Guid("cb84c10e-d817-4ed8-a66c-000000000004"), "Relax", HueResourceType.Scene);

        _resources =
        [
            new HueResourceGroup(new Guid("cb84c10e-d817-4ed8-a66c-000000000005"), "Office", HueResourceType.Room,
                [desk, ceiling, focus, relax]),
            new HueResourceGroup(new Guid("cb84c10e-d817-4ed8-a66c-000000000006"), "Upstairs", HueResourceType.Zone,
                []),
            desk, ceiling, focus, relax
        ];
    }

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
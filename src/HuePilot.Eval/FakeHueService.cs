using HuePilot.Application;

/// <summary>A test double that exposes a fixed resource catalog and records any attempted mutation.</summary>
internal sealed class FakeHueService(IReadOnlyList<HueResource> resources) : IHueService
{
    public List<HueAction> Executed { get; } = [];

    public Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(resources);

    public Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default)
    {
        Executed.Add(action);
        return Task.CompletedTask;
    }
}

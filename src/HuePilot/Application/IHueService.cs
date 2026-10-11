namespace HuePilot.Application;

/// <summary>Application boundary for reading Hue resources and executing validated actions.</summary>
public interface IHueService
{
    /// <summary>Loads the rooms, lights, scenes, and their group associations.</summary>
    /// <param name="cancellationToken">Cancels the Bridge request when the caller is canceled.</param>
    /// <returns>The resources currently available from Hue.</returns>
    Task<IReadOnlyList<HueResource>> GetResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes one action that has already been resolved and validated by HuePilot.</summary>
    /// <param name="action">The allowlisted operation and exact discovered resource.</param>
    /// <param name="cancellationToken">Cancels the Bridge request when the caller is canceled.</param>
    Task ExecuteAsync(HueAction action, CancellationToken cancellationToken = default);
}

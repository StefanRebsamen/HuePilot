using HueApi;
using HueApi.BridgeLocator;

namespace HuePilot.Infrastructure;

/// <summary>Discovers Hue Bridges and performs the physical-button pairing handshake.</summary>
public sealed class HueBridgeSetupService
{
    /// <summary>Looks up Hue Bridges available to the Hue discovery service.</summary>
    /// <param name="cancellationToken">Cancels the discovery request.</param>
    /// <returns>Discovered Bridge addresses and IDs.</returns>
    public async Task<IReadOnlyList<HueBridgeInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var locator = new HttpBridgeLocator();
        var bridges = await locator.LocateBridgesAsync(cancellationToken);
        return bridges.Select(x => new HueBridgeInfo(x.IpAddress, x.BridgeId)).ToArray();
    }

    /// <summary>Registers HuePilot with a Bridge after the user presses its physical link button.</summary>
    /// <param name="ipAddress">The Bridge's local IPv4 or IPv6 address.</param>
    /// <param name="cancellationToken">Cancels the registration request.</param>
    /// <returns>The Bridge application key; callers must store it as a secret.</returns>
    public async Task<string> PairAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!System.Net.IPAddress.TryParse(ipAddress, out _))
            throw new ArgumentException("Enter a valid local Bridge IP address.", nameof(ipAddress));

        var result = await LocalHueApi.RegisterAsync(ipAddress, "HuePilot", "HuePilot", false, cancellationToken);
        return result?.Username ?? throw new InvalidOperationException("The Bridge did not return an application key.");
    }
}

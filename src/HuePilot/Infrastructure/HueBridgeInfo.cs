namespace HuePilot.Infrastructure;

/// <summary>Bridge address and stable Bridge identifier returned by discovery.</summary>
/// <param name="IpAddress">The local network address used by the Hue API.</param>
/// <param name="BridgeId">The Bridge identifier returned by Hue discovery.</param>
public sealed record HueBridgeInfo(string IpAddress, string BridgeId);

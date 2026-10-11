namespace HuePilot.Application;

/// <summary>A Hue room or zone associated with a light or scene.</summary>
/// <param name="Id">The Bridge resource ID.</param>
/// <param name="Name">The displayed room or zone name.</param>
/// <param name="Type">The group kind, normally <c>room</c> or <c>zone</c>.</param>
public sealed record HueResourceGroup(string Id, string Name, string Type);

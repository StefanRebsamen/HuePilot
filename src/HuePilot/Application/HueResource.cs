namespace HuePilot.Application;

/// <summary>A Hue resource discovered from the Bridge or supplied by the demo service.</summary>
/// <param name="Id">The stable resource ID used for validated operations.</param>
/// <param name="Name">The user-visible name.</param>
/// <param name="Type">The resource kind: room, light, or scene.</param>
/// <param name="Groups">Rooms or zones associated with this resource.</param>
public sealed record HueResource(string Id, string Name, string Type, IReadOnlyList<HueResourceGroup>? Groups = null);

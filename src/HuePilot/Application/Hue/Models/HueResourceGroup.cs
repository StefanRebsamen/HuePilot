namespace HuePilot.Application.Hue.Models;

/// <summary>A Hue room or zone with its member lights and scenes.</summary>
/// <param name="Id">The Bridge resource ID.</param>
/// <param name="Name">The displayed room or zone name.</param>
/// <param name="Type">The group kind, normally <c>room</c> or <c>zone</c>.</param>
/// <param name="Resources">The discovered lights and scenes belonging to the group.</param>
public sealed record HueResourceGroup(Guid Id, string Name, HueResourceType Type, IReadOnlyList<HueResource> Resources)
    : HueResource(Id, Name, Type)
{
    /// <summary>Checks whether the group contains a member with the supplied Hue resource ID.</summary>
    public bool ContainsResource(Guid resourceId)
    {
        return Resources.Any(member => member.Id == resourceId);
    }
}
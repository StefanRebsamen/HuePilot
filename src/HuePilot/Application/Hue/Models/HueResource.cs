namespace HuePilot.Application.Hue.Models;

/// <summary>A Hue resource discovered from the Bridge or supplied by the demo service.</summary>
/// <param name="Id">The stable resource ID used for validated operations.</param>
/// <param name="Name">The user-visible name.</param>
/// <param name="Type">The resource kind.</param>
public record HueResource(Guid Id, string Name, HueResourceType Type)
{
    /// <summary>
    /// </summary>
    /// <param name="resources"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    internal static HueResource? FindUniqueById(IReadOnlyList<HueResource> resources, Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var matches = resources.Where(resource => resource.Id == id).Take(2).ToArray();
        return matches is [var match] ? match : null;
    }
}
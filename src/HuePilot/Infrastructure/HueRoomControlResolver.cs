using HueApi.Models;
using ApiGroupedLight = HueApi.Models.GroupedLight;
using ApiRoom = HueApi.Models.Room;

namespace HuePilot.Infrastructure;

/// <summary>Resolves the grouped-light service that controls a Hue room.</summary>
public static class HueRoomControlResolver
{
    /// <summary>Finds the unique grouped-light service owned by a room or zone.</summary>
    /// <param name="groupId">The room or zone resource ID.</param>
    /// <param name="groupType">The owner resource type, either room or zone.</param>
    /// <param name="groupedLights">Grouped-light resources returned by the Bridge.</param>
    public static Guid? FindGroupedLightId(Guid groupId, string groupType, IEnumerable<ApiGroupedLight> groupedLights)
    {
        var matches = groupedLights
            .Where(groupedLight => groupedLight.Owner is { } owner &&
                owner.Rid == groupId &&
                owner.Rtype.Equals(groupType, StringComparison.OrdinalIgnoreCase))
            .Select(groupedLight => groupedLight.Id)
            .Distinct()
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    /// <summary>
    /// Finds the unique grouped-light resource whose owner is the specified room.
    /// This is a fallback for Bridges that omit the room's <c>grouped_services</c> collection.
    /// </summary>
    /// <param name="room">The Hue room whose grouped-light service is needed.</param>
    /// <param name="groupedLights">Grouped-light service resources returned by the Bridge.</param>
    /// <returns>The unique owned grouped-light ID, or <see langword="null"/> if none or multiple match.</returns>
    public static Guid? FindGroupedLightId(ApiRoom room, IEnumerable<ApiGroupedLight> groupedLights)
    {
        // Some Bridge firmware omits room.grouped_services, but each grouped_light still names
        // its owning room. Require exactly one owner match rather than guessing.
        return FindGroupedLightId(room.Id, "room", groupedLights);
    }
}

using System.ComponentModel;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using HuePilot.Application;
using ModelContextProtocol.Server;

namespace HuePilot.Mcp;

[McpServerToolType]
public static class HueMcpTools
{
    [McpServerTool(Name = "list_hue_resources", Title = "List Hue rooms, lights, and scenes"),
     Description("Read-only. List Hue resources and their IDs. Use a returned resource ID with the control tools; never guess an ID.")]
    public static async Task<string> ListHueResources(
        [Description("Filter by resource type: all, room, light, or scene.")] string type = "all",
        IHueService? hueService = null,
        CancellationToken cancellationToken = default)
    {
        if (hueService is null) throw new InvalidOperationException("Hue service is unavailable.");
        if (type is not ("all" or "room" or "light" or "scene"))
            return "Not applied: type must be all, room, light, or scene.";

        try
        {
            var resources = await hueService.GetResourcesAsync(cancellationToken);
            var selected = resources
                .Where(resource => type == "all" || resource.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                .OrderBy(resource => resource.Type)
                .ThenBy(resource => resource.Name, StringComparer.OrdinalIgnoreCase)
            .Select(resource => new
            {
                resource.Id,
                resource.Name,
                resource.Type,
                groups = (resource.Groups ?? []).Select(group => new { group.Id, group.Name, group.Type })
            });
            return JsonSerializer.Serialize(selected, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            return BridgeUnavailable(ex);
        }
    }

    [McpServerTool(Name = "set_hue_power", Title = "Turn a Hue light or room on or off"),
     Description("Changes power for one discovered Hue light or room. This controls real devices. Call list_hue_resources first and use its exact ID.")]
    public static Task<string> SetHuePower(
        [Description("Exact ID of a discovered light or room from list_hue_resources.")] string resourceId,
        [Description("True to turn on; false to turn off.")] bool on,
        IHueService? hueService = null,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(resourceId, on ? HueOperation.TurnOn : HueOperation.TurnOff, null, null, null, ["light", "room"], hueService, cancellationToken);

    [McpServerTool(Name = "set_hue_brightness", Title = "Set Hue brightness"),
     Description("Sets brightness on one discovered Hue light or room to a whole-number percentage from 1 to 100. This controls real devices. Use an exact ID from list_hue_resources.")]
    public static Task<string> SetHueBrightness(
        [Description("Exact ID of a discovered light or room from list_hue_resources.")] string resourceId,
        [Description("Brightness percentage, from 1 to 100 inclusive.")] int brightnessPercent,
        IHueService? hueService = null,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(resourceId, HueOperation.SetBrightness, brightnessPercent, null, null, ["light", "room"], hueService, cancellationToken);

    [McpServerTool(Name = "set_hue_color", Title = "Set Hue color"),
     Description("Sets a discovered Hue light or room to a supported named color: red, orange, yellow, green, cyan, blue, purple, pink, white, warm white, or cool white. This controls real devices. Use an exact ID from list_hue_resources.")]
    public static Task<string> SetHueColor(
        [Description("Exact ID of a discovered light or room from list_hue_resources.")] string resourceId,
        [Description("Supported named color: red, orange, yellow, green, cyan, blue, purple, pink, white, warm white, or cool white.")] string color,
        IHueService? hueService = null,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(resourceId, HueOperation.SetColor, null, color, null, ["light", "room"], hueService, cancellationToken);

    [McpServerTool(Name = "activate_hue_scene", Title = "Activate an existing Hue scene"),
     Description("Activates one existing Hue scene on its associated room or zone. This controls real devices. Call list_hue_resources with type scene and use the exact ID. Optionally provide roomId to verify the scene is associated with that room.")]
    public static Task<string> ActivateHueScene(
        [Description("Exact ID of an existing scene from list_hue_resources.")] string sceneId,
        [Description("Optional exact room ID from list_hue_resources. If supplied, the scene must be associated with this room.")] string? roomId = null,
        IHueService? hueService = null,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(sceneId, HueOperation.ActivateScene, null, null, roomId, ["scene"], hueService, cancellationToken);

    private static async Task<string> ApplyAsync(
        string resourceId,
        HueOperation operation,
        int? brightnessPercent,
        string? color,
        string? requiredRoomId,
        string[] allowedTypes,
        IHueService? hueService,
        CancellationToken cancellationToken)
    {
        if (hueService is null) return "Not applied: Hue service is unavailable.";

        try
        {
            // Refresh before every mutation so stale IDs or resources removed from the Bridge are rejected.
            var resources = await hueService.GetResourcesAsync(cancellationToken);
            var matches = resources.Where(resource => resource.Id.Equals(resourceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
                return "Not applied: that ID is not a currently discovered Hue resource. List resources and try again.";
            if (matches.Length != 1)
                return "Not applied: the resource ID was not unique.";

            var target = matches[0];
            if (!allowedTypes.Contains(target.Type, StringComparer.OrdinalIgnoreCase))
                return $"Not applied: {operation} cannot be used on a {target.Type}.";

            if (!string.IsNullOrWhiteSpace(requiredRoomId) && !(target.Groups ?? []).Any(group =>
                    group.Id.Equals(requiredRoomId, StringComparison.OrdinalIgnoreCase) && group.Type.Equals("room", StringComparison.OrdinalIgnoreCase)))
                return "Not applied: that scene is not associated with the requested room. List resources to see its room associations.";

            var intent = new HueIntent(operation, target.Name, target.Type, brightnessPercent, color);
            if (!HueActionValidator.TryCreate(intent, target, out var action, out var validationError))
                return $"Not applied: {validationError}";

            await hueService.ExecuteAsync(action!, cancellationToken);
            return $"Applied {operation} to {target.Type} ‘{target.Name}’.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return BridgeUnavailable(ex);
        }
        catch
        {
            return "The Hue Bridge could not complete the action. Check Bridge connectivity and the resource's capabilities.";
        }
    }

    private static string BridgeUnavailable(HttpRequestException exception)
    {
        var socketError = exception.InnerException as SocketException;
        return socketError is null
            ? "Could not reach the Hue Bridge. Verify its IP address, application key, and local-network connection."
            : $"Could not reach the Hue Bridge (network error: {socketError.SocketErrorCode}). Verify the Bridge IP and that this MCP server is on the same reachable local network.";
    }
}

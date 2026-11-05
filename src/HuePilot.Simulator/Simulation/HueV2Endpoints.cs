namespace HuePilot.Simulator.Simulation;

public static class HueV2Endpoints
{
    public static void MapHueV2Endpoints(this WebApplication app)
    {
        app.MapGet("/clip/v2/resource/{resourceType}", (string resourceType, SimulatorState state) =>
        {
            var snapshot = state.GetSnapshot();
            object[]? data = resourceType switch
            {
                "light" => snapshot.Lights.Select(ToLight).Cast<object>().ToArray(),
                "room" => snapshot.Rooms.Select(group => ToGroup(group, "room")).Cast<object>().ToArray(),
                "zone" => snapshot.Zones.Select(group => ToGroup(group, "zone")).Cast<object>().ToArray(),
                "grouped_light" => snapshot.GroupedLights.Select(ToGroupedLight).Cast<object>().ToArray(),
                "device" => snapshot.Lights.Select(ToDevice).Cast<object>().ToArray(),
                "scene" => snapshot.Scenes.Select(scene => ToScene(scene, snapshot)).Cast<object>().ToArray(),
                _ => null
            };

            return data is null
                ? Results.NotFound(new { errors = new[] { new { description = "Unsupported simulated resource type." } } })
                : Results.Ok(new { data });
        });

        app.MapPut("/clip/v2/resource/light/{id:guid}", (Guid id, HueUpdate update, SimulatorState state) =>
            ApplyLightUpdate(id, update, state));
        app.MapPut("/clip/v2/resource/grouped_light/{id:guid}", (Guid id, HueUpdate update, SimulatorState state) =>
            ApplyGroupedLightUpdate(id, update, state));
        app.MapPut("/clip/v2/resource/scene/{id:guid}", (Guid id, HueUpdate update, SimulatorState state) =>
            update.Recall?.Action == "active" && state.ActivateScene(id)
                ? Results.Ok(new { data = new[] { new { rid = id, rtype = "scene" } } })
                : Results.BadRequest(new { errors = new[] { new { description = "Unsupported simulated scene update." } } }));
    }

    private static IResult ApplyLightUpdate(Guid id, HueUpdate update, SimulatorState state)
    {
        var request = ToLightUpdate(update);
        return request is not null && state.UpdateLight(id, request)
            ? Results.Ok(new { data = new[] { new { rid = id, rtype = "light" } } })
            : Results.BadRequest(new { errors = new[] { new { description = "Invalid simulated light update." } } });
    }

    private static IResult ApplyGroupedLightUpdate(Guid id, HueUpdate update, SimulatorState state)
    {
        var request = ToLightUpdate(update);
        return request is not null && state.UpdateGroupedLight(id,
            new GroupedLightUpdateRequest(request.On, request.Brightness, request.Color))
            ? Results.Ok(new { data = new[] { new { rid = id, rtype = "grouped_light" } } })
            : Results.BadRequest(new { errors = new[] { new { description = "Invalid simulated grouped-light update." } } });
    }

    private static LightUpdateRequest? ToLightUpdate(HueUpdate update)
    {
        var color = update.Color?.Xy is { } xy ? new HueColor(xy.X, xy.Y) : null;
        return update.On is null && update.Dimming is null && update.Color is null
            ? null
            : new LightUpdateRequest(update.On?.On, update.Dimming?.Brightness, color);
    }

    private static object ToLight(SimulatedLight light) => new
    {
        id = light.Id, type = "light", metadata = new { name = light.Name },
        on = new { on = light.IsOn }, dimming = new { brightness = light.Brightness },
        color = light.Color is null ? null : new { xy = new { x = light.Color.X, y = light.Color.Y } }
    };

    private static object ToGroup(SimulatedGroup group, string type) => new
    {
        id = group.Id, type, metadata = new { name = group.Name },
        children = group.LightIds.Select(id => new { rid = id, rtype = "light" }),
        services = new[] { new { rid = group.GroupedLightId, rtype = "grouped_light" } }
    };

    private static object ToGroupedLight(SimulatedGroupedLight group) => new
    {
        id = group.Id, type = "grouped_light", owner = new { rid = group.OwnerId, rtype = group.OwnerType },
        on = new { on = group.IsOn }, dimming = new { brightness = group.Brightness },
        color = group.Color is null ? null : new { xy = new { x = group.Color.X, y = group.Color.Y } }
    };

    private static object ToDevice(SimulatedLight light) => new
    {
        id = light.Id, type = "device", metadata = new { name = light.Name },
        services = new[] { new { rid = light.Id, rtype = "light" } }
    };

    private static object ToScene(SimulatedScene scene, SimulatorSnapshot snapshot)
    {
        var owner = snapshot.Rooms.Select(group => (Group: group, Type: "room"))
            .Concat(snapshot.Zones.Select(group => (Group: group, Type: "zone")))
            .FirstOrDefault(candidate => scene.LightIds.All(candidate.Group.LightIds.Contains));
        return new
        {
            id = scene.Id, type = "scene", metadata = new { name = scene.Name },
            group = new { rid = owner.Group?.Id ?? Guid.Empty, rtype = owner.Type ?? "room" }
        };
    }

    public sealed record HueUpdate(HueOn? On, HueDimming? Dimming, HueColorUpdate? Color, HueRecall? Recall);
    public sealed record HueOn(bool On);
    public sealed record HueDimming(double Brightness);
    public sealed record HueColorUpdate(HueXy? Xy);
    public sealed record HueXy(double X, double Y);
    public sealed record HueRecall(string? Action);
}

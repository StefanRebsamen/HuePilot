namespace HuePilot.Simulator.Simulation;

public sealed class SimulatorState
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, SimulatedLight> _lights;
    private readonly IReadOnlyList<SimulatedGroup> _rooms;
    private readonly IReadOnlyList<SimulatedGroup> _zones;
    private readonly IReadOnlyList<SimulatedScene> _scenes;

    private SimulatorState(SimulatorSnapshot fixtures)
    {
        _lights = fixtures.Lights.ToDictionary(light => light.Id);
        _rooms = fixtures.Rooms.ToArray();
        _zones = fixtures.Zones.ToArray();
        _scenes = fixtures.Scenes.ToArray();
    }

    public static SimulatorState CreateDefault() => new(SimulatorFixtures.Create());

    public SimulatorSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var lights = _lights.Values.OrderBy(light => light.Name, StringComparer.Ordinal).ToArray();
            var groupedLights = _rooms.Select(group => ToGroupedLight(group, "room", lights))
                .Concat(_zones.Select(group => ToGroupedLight(group, "zone", lights))).ToArray();
            return new SimulatorSnapshot(lights, _rooms.ToArray(), _zones.ToArray(), groupedLights, _scenes.ToArray());
        }
    }

    public bool UpdateLight(Guid id, LightUpdateRequest update)
    {
        lock (_gate)
        {
            if (!_lights.TryGetValue(id, out var light) || !IsValid(update.On, update.Brightness, update.Color))
            {
                return false;
            }

            _lights[id] = light with
            {
                IsOn = update.On ?? light.IsOn,
                Brightness = update.Brightness ?? light.Brightness,
                Color = update.Color ?? light.Color
            };
            return true;
        }
    }

    public bool UpdateGroupedLight(Guid id, GroupedLightUpdateRequest update)
    {
        lock (_gate)
        {
            var group = _rooms.Concat(_zones).SingleOrDefault(candidate => candidate.GroupedLightId == id);
            if (group is null || !IsValid(update.On, update.Brightness, update.Color))
            {
                return false;
            }

            foreach (var lightId in group.LightIds)
            {
                var light = _lights[lightId];
                _lights[lightId] = light with
                {
                    IsOn = update.On ?? light.IsOn,
                    Brightness = update.Brightness ?? light.Brightness,
                    Color = update.Color ?? light.Color
                };
            }

            return true;
        }
    }

    public bool ActivateScene(Guid id)
    {
        lock (_gate)
        {
            var scene = _scenes.SingleOrDefault(candidate => candidate.Id == id);
            if (scene is null)
            {
                return false;
            }

            foreach (var lightId in scene.LightIds)
            {
                var light = _lights[lightId];
                _lights[lightId] = light with { IsOn = true, Brightness = scene.Brightness, Color = scene.Color };
            }

            return true;
        }
    }

    private static SimulatedGroupedLight ToGroupedLight(SimulatedGroup group, string ownerType,
        IReadOnlyList<SimulatedLight> lights)
    {
        var members = lights.Where(light => group.LightIds.Contains(light.Id)).ToArray();
        var active = members.Where(light => light.IsOn).ToArray();
        return new SimulatedGroupedLight(group.GroupedLightId, group.Id, ownerType, active.Length > 0,
            active.Length == 0 ? 0 : active.Average(light => light.Brightness), active.FirstOrDefault()?.Color);
    }

    private static bool IsValid(bool? on, double? brightness, HueColor? color)
    {
        if (on is null && brightness is null && color is null)
        {
            return false;
        }

        return (brightness is null || double.IsFinite(brightness.Value) && brightness is >= 1 and <= 100) &&
               (color is null || double.IsFinite(color.X) && double.IsFinite(color.Y) && color.X is >= 0 and <= 1 &&
                color.Y is >= 0 and <= 1);
    }
}

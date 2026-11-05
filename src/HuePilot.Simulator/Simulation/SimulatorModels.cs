namespace HuePilot.Simulator.Simulation;

public sealed record HueColor(double X, double Y);

public sealed record LightUpdateRequest(bool? On, double? Brightness, HueColor? Color);

public sealed record GroupedLightUpdateRequest(bool? On, double? Brightness, HueColor? Color);

public sealed record SimulatedLight(Guid Id, string Name, bool IsOn, double Brightness, HueColor? Color);

public sealed record SimulatedGroup(Guid Id, string Name, Guid GroupedLightId, IReadOnlyList<Guid> LightIds);

public sealed record SimulatedGroupedLight(Guid Id, Guid OwnerId, string OwnerType, bool IsOn, double Brightness,
    HueColor? Color);

public sealed record SimulatedScene(Guid Id, string Name, IReadOnlyList<Guid> LightIds, double Brightness,
    HueColor? Color);

public sealed record SimulatorSnapshot(IReadOnlyList<SimulatedLight> Lights, IReadOnlyList<SimulatedGroup> Rooms,
    IReadOnlyList<SimulatedGroup> Zones, IReadOnlyList<SimulatedGroupedLight> GroupedLights,
    IReadOnlyList<SimulatedScene> Scenes);

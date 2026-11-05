# Hue Bridge Simulator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a separate Blazor application that simulates the Hue v2 subset HuePilot uses, with a live visual dashboard.

**Architecture:** `HuePilot.Simulator` hosts minimal Hue v2 endpoints and a server-interactive Blazor dashboard over one singleton `SimulatorState`. The resource graph has stable GUID identifiers and preserves device, room/zone, scene, and grouped-light relationships required by HuePilot’s existing production adapter. Tests first exercise state semantics, endpoint envelopes, then the adapter over a real loopback HTTPS host.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Blazor Web App with server interactivity, xUnit, existing `HueApi` client.

**Spec:** `docs/superpowers/specs/2026-11-05-hue-bridge-simulator-design.md`

## Global Constraints

- Add `HuePilot.Simulator` as a separate project in `src/HuePilot.sln`; do not merge bridge endpoints into HuePilot.
- Implement only HuePilot’s used Hue v2 resources: light, room, zone, scene, device, and grouped_light; support only light, grouped-light, and scene updates.
- Use `/clip/v2/resource/{type}` Hue-style `data` envelopes and stable GUID resource IDs.
- Keep all simulator state in memory, deterministic, and reset on process restart; never require a Bridge, AI service, authentication, pairing, or persistence.
- Treat update requests as untrusted: reject unknown IDs, malformed or unsupported bodies, non-finite/out-of-range values, and invalid color coordinates without partial mutation.
- Dashboard and API share one concurrency-safe state store; dashboard polling reflects changes made through HuePilot.
- Keep Bridge and provider credentials out of browser code and documentation examples.

## Review Focus

- A simulator address with a non-default local HTTPS port must be accepted by `LocalHueApi`; Task 2’s loopback adapter test pins the exact `Bridge:IpAddress` format.
- A zone’s grouped light must declare its owner relationship; Task 3’s discovery fixture validates room and zone group resolution through `HueApiHueService`.
- An update with a valid ID but invalid brightness or color must leave state unchanged; Task 1 owns state mutation rejection tests and Task 4 owns HTTP rejection tests.
- A scene activation or individual-light change must recompute overlapping room/zone status; Task 1 pins aggregate state after both mutation paths.
- Concurrent API and dashboard reads must serialize complete snapshots, never partial collections; Task 1 adds a parallel mutation/snapshot test.

---

## File Structure

- `HuePilot.Simulator/HuePilot.Simulator.csproj` — standalone Blazor Web App project.
- `HuePilot.Simulator/Program.cs` — dashboard setup, `SimulatorState` registration, and Hue v2 endpoint mapping.
- `HuePilot.Simulator/Simulation/SimulatorState.cs` — immutable resource catalog snapshots and atomic mutation operations.
- `HuePilot.Simulator/Simulation/SimulatorModels.cs` — internal simulation records and Hue response/update DTOs.
- `HuePilot.Simulator/Simulation/SimulatorFixtures.cs` — deterministic rooms, zones, devices, lights, grouped lights, and scenes.
- `HuePilot.Simulator/Components/Pages/Home.razor` — live presentation dashboard with polling and light controls.
- `HuePilot.Simulator/Components/Pages/Home.razor.css` — local dashboard styling.
- `HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj` — offline simulator test project.
- `HuePilot.Simulator.Tests/SimulatorStateTests.cs` — state mutation, group aggregation, scene, validation, and concurrency tests.
- `HuePilot.Simulator.Tests/HueV2EndpointTests.cs` — loopback API resource and update-envelope tests.
- `HuePilot.Simulator.Tests/HuePilotCompatibilityTests.cs` — existing `HueApiHueService` against the simulator host.
- `HuePilot.sln` — includes simulator and simulator-test projects.
- `README.md` — presentation start-up and HuePilot connection instructions.

### Task 1: Scaffold Simulator and Implement Atomic Simulation State

**Files:**
- Create: `HuePilot.Simulator/HuePilot.Simulator.csproj`
- Create: `HuePilot.Simulator/Program.cs`
- Create: `HuePilot.Simulator/Simulation/SimulatorModels.cs`
- Create: `HuePilot.Simulator/Simulation/SimulatorFixtures.cs`
- Create: `HuePilot.Simulator/Simulation/SimulatorState.cs`
- Create: `HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj`
- Create: `HuePilot.Simulator.Tests/SimulatorStateTests.cs`
- Modify: `HuePilot.sln`

**Interfaces:**
- Produces: `SimulatorState.GetSnapshot() -> SimulatorSnapshot` and mutation operations `UpdateLight(Guid, LightUpdateRequest)`, `UpdateGroupedLight(Guid, GroupedLightUpdateRequest)`, and `ActivateScene(Guid)`.
- Produces: deterministic `SimulatorFixtures.Create()` resources with light, room, zone, scene, device, and grouped-light relationships.

- [ ] **Step 1: Write failing state tests**

```csharp
[Fact]
public void UpdateGroupedLight_ValidBrightness_UpdatesEveryOwnedLightAndDerivedGroupState()
{
    var state = SimulatorState.CreateDefault();
    var zone = state.GetSnapshot().Zones.Single();

    state.UpdateGroupedLight(zone.GroupedLightId, new GroupedLightUpdateRequest(On: true, Brightness: 65, Color: null));

    Assert.All(state.GetSnapshot().Lights.Where(light => zone.LightIds.Contains(light.Id)),
        light => Assert.Equal(65, light.Brightness));
}
```

Include tests for a scene preset, invalid mutation preserving the snapshot, and parallel snapshot/mutation access.

- [ ] **Step 2: Run state tests to verify they fail**

Run: `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter FullyQualifiedName~SimulatorStateTests`

Expected: FAIL because simulator project and state interfaces do not exist.

- [ ] **Step 3: Create the solution projects and implement state interfaces**

Create a .NET 10 Blazor Web App using server interactivity and a test project referencing it. Add both to the solution. Implement a lock-protected state store that creates deep-copy snapshots before returning; model a group as on if any member light is on and its brightness as the average of on member lights. Store Hue color coordinates and validate finite coordinates and brightness 1–100.

- [ ] **Step 4: Run state tests to verify they pass**

Run: `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter FullyQualifiedName~SimulatorStateTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HuePilot.sln HuePilot.Simulator HuePilot.Simulator.Tests
git commit -m "feat: add Hue simulator state"
```

### Task 2: Implement Hue v2 Discovery Endpoints and Confirm Client Address Compatibility

**Files:**
- Modify: `HuePilot.Simulator/Program.cs`
- Create: `HuePilot.Simulator/Simulation/HueV2Endpoints.cs`
- Create: `HuePilot.Simulator.Tests/HueV2EndpointTests.cs`

**Interfaces:**
- Consumes: `SimulatorState.GetSnapshot() -> SimulatorSnapshot`.
- Produces: `MapHueV2Endpoints(this WebApplication app)` with GET routes for light, room, zone, scene, device, and grouped_light.

- [ ] **Step 1: Write failing discovery and connection tests**

```csharp
[Fact]
public async Task GetRooms_ReturnsHueDataEnvelopeWithGroupedLightService()
{
    var response = await _client.GetFromJsonAsync<HueEnvelope<RoomResponse>>("/clip/v2/resource/room");

    Assert.NotEmpty(response!.Data);
    Assert.Contains(response.Data[0].Services, service => service.Rtype == "grouped_light");
}

[Fact]
public async Task LocalHueApi_LocalhostWithPort_CanReadSimulatorLights()
{
    var api = new LocalHueApi(_simulatorHostAndPort, "presentation-key");

    Assert.NotEmpty((await api.Light.GetAllAsync()).Data);
}
```

Host the test server over a trusted loopback HTTPS endpoint. Capture the exact accepted simulator address format for the run guide.

- [ ] **Step 2: Run discovery tests to verify they fail**

Run: `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter FullyQualifiedName~HueV2EndpointTests`

Expected: FAIL because the Hue routes are not mapped.

- [ ] **Step 3: Implement `MapHueV2Endpoints` discovery routes**

Return exact Hue v2 JSON field names consumed by `HueApi`: resource `id`, `type`, metadata, services/children, owner, on, dimming, and color fields. Each route returns `{ "data": [...] }`; unknown resource types return the Hue-style error envelope selected in Task 4.

- [ ] **Step 4: Run discovery tests to verify they pass**

Run: `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter FullyQualifiedName~HueV2EndpointTests`

Expected: PASS, including `LocalHueApi` against localhost plus port.

- [ ] **Step 5: Commit**

```bash
git add HuePilot.Simulator HuePilot.Simulator.Tests
git commit -m "feat: expose simulated Hue discovery API"
```

### Task 3: Implement Mutation Endpoints and Production-Adapter Compatibility

**Files:**
- Modify: `HuePilot.Simulator/Simulation/HueV2Endpoints.cs`
- Create: `HuePilot.Simulator.Tests/HuePilotCompatibilityTests.cs`
- Modify: `HuePilot.Simulator.Tests/HueV2EndpointTests.cs`

**Interfaces:**
- Consumes: `SimulatorState.UpdateLight`, `UpdateGroupedLight`, and `ActivateScene` from Task 1.
- Produces: PUT routes for light, grouped_light, and scene resource IDs.

- [ ] **Step 1: Write failing endpoint and adapter tests**

```csharp
[Fact]
public async Task HueApiHueService_ExecuteAsync_ChangesLightBrightnessThroughSimulator()
{
    var service = CreateProductionHueService(_simulatorHostAndPort);
    var light = (await service.GetResourcesAsync()).Single(resource => resource.Type == HueResourceTypes.Light);

    await service.ExecuteAsync(new HueAction(HueOperation.SetBrightness, light, 40));

    Assert.Contains("40%", await service.GetStatusAsync(light));
}
```

Add cases for individual/group power, grouped color, scene activation, room/zone status, unknown IDs, malformed JSON, unsupported properties, invalid brightness, and invalid color coordinates.

- [ ] **Step 2: Run mutation tests to verify they fail**

Run: `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter "FullyQualifiedName~HueV2EndpointTests|FullyQualifiedName~HuePilotCompatibilityTests"`

Expected: FAIL because mutation routes are absent.

- [ ] **Step 3: Implement validated Hue update routes**

Parse only Hue bodies emitted by the installed `HueApi` library. On valid input, atomically apply the matching state operation and return a Hue-style success `data` identity. On invalid input, return the consistent error envelope without changing state.

- [ ] **Step 4: Run mutation and adapter tests to verify they pass**

Run: `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter "FullyQualifiedName~HueV2EndpointTests|FullyQualifiedName~HuePilotCompatibilityTests"`

Expected: PASS for discovery, all HuePilot-supported operations, and rejection cases.

- [ ] **Step 5: Commit**

```bash
git add HuePilot.Simulator HuePilot.Simulator.Tests
git commit -m "feat: simulate Hue control API"
```

### Task 4: Build the Presentation Dashboard and Document Running the Demo

**Files:**
- Create: `HuePilot.Simulator/Components/App.razor`
- Create: `HuePilot.Simulator/Components/Routes.razor`
- Create: `HuePilot.Simulator/Components/Pages/Home.razor`
- Create: `HuePilot.Simulator/Components/Pages/Home.razor.css`
- Create: `HuePilot.Simulator/Components/_Imports.razor`
- Modify: `HuePilot.Simulator/Program.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `SimulatorState.GetSnapshot()` and its mutation operations from Task 1.
- Produces: a dashboard that displays rooms/zones, their lights, power, brightness, color, and scene controls.

- [ ] **Step 1: Write a failing dashboard-state test**

```csharp
[Fact]
public void Snapshot_LightChangedThroughState_IsVisibleToDashboardProjection()
{
    var state = SimulatorState.CreateDefault();
    var light = state.GetSnapshot().Lights.First();

    state.UpdateLight(light.Id, new LightUpdateRequest(On: true, Brightness: 75, Color: null));

    Assert.Equal(75, state.GetSnapshot().Lights.Single(item => item.Id == light.Id).Brightness);
}
```

This is the dashboard’s observable data contract; retain it alongside the state tests if it overlaps.

- [ ] **Step 2: Run the focused test to verify it fails only if the projection is missing**

Run: `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter FullyQualifiedName~SimulatorStateTests`

Expected: PASS if Task 1 already covers the contract; otherwise add the smallest missing projection behavior and observe failure before it is added.

- [ ] **Step 3: Implement the server-interactive dashboard and presentation guide**

Render a clear room/zone layout, light state, and scene buttons. Use a short disposable polling timer to refresh snapshots after HuePilot-triggered API mutations. Add README instructions for starting the simulator, reading its HTTPS launch URL, configuring `Bridge:IpAddress` to the verified host-and-port format, setting any non-empty `Bridge:ApplicationKey`, and starting HuePilot. State explicitly that the simulator is local, in-memory, and resets on restart.

- [ ] **Step 4: Run the full suite and build the solution**

Run: `dotnet test HuePilot.sln --no-restore`

Expected: PASS.

Run: `dotnet build HuePilot.sln --no-restore`

Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add HuePilot.Simulator README.md HuePilot.sln
git commit -m "feat: add Hue simulator dashboard"
```

## Self-review

- Spec coverage: Tasks 1–4 cover the separate project, deterministic state, discovery and mutation subset, group/scene relationships, Blazor dashboard, validation, compatibility testing, and run instructions.
- Step scan: Each task has one red-green implementation increment and verification command; interfaces are defined by the preceding state or endpoint task.
- Type consistency: `SimulatorState` owns immutable `SimulatorSnapshot` reads and all mutations; endpoint and UI layers consume only that public state boundary.
- Review focus: Every listed failure mode has a named test owner in Tasks 1–3.
- Proportion: The plan supplies file ownership and interfaces without prescribing DTO implementation bodies before their contract tests characterize the installed HueApi package.

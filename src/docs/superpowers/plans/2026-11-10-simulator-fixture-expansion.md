# Simulator Fixture Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the simulator's deterministic fixture with indoor and garden rooms, additional lamps, and overlapping zones.

**Architecture:** Keep the entire catalog declarative in `SimulatorFixtures.Create()`. Continue deriving grouped-light state from group membership in `SimulatorState`; only fixture data and its behavioral assertions change.

**Tech Stack:** .NET 10, C#, xUnit, ASP.NET Core test host.

**Spec:** `docs/superpowers/specs/2026-11-10-simulator-fixtures-design.md`

## Global Constraints

- Preserve existing fixture resources and simulator behavior.
- Use deterministic unique GUIDs and valid group memberships.
- Do not add persistence, UI behavior, endpoint changes, or live Hue dependencies.

## Review Focus

- A Garden room contains exactly its five named lamp IDs; assert its count and membership in Task 1.
- Every room and zone refers only to a fixture light; assert no dangling IDs in Task 1.
- Zone grouped lights retain `OwnerType == "zone"`; preserve and expand the existing assertion in Task 1.
- Zone updates modify every member even when members belong to different rooms; use a multi-room zone in Task 1.
- Fixture resources remain independently recreatable per `SimulatorState.CreateDefault()`; run the full simulator suite in Task 2.

---

### Task 1: Expand and validate the deterministic fixture catalog

**Files:**
- Modify: `HuePilot.Simulator/Simulation/SimulatorFixtures.cs`
- Modify: `HuePilot.Simulator.Tests/SimulatorStateTests.cs`

**Interfaces:**
- Consumes: `SimulatorSnapshot`, `SimulatedLight`, and `SimulatedGroup` fixture models.
- Produces: `SimulatorFixtures.Create()` with six rooms, eleven lights, and three zones, including Garden with five lamps and cross-room zones.

- [ ] **Step 1: Write failing catalog and membership tests**

Add `CreateDefault_ExpandedCatalogHasGardenAndOverlappingZones` to assert 6 rooms, 11 lights, 3 zones, a Garden room with 5 lights, and that every room/zone `LightIds` member is in `snapshot.Lights`.

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `dotnet test HuePilot.Simulator.Tests --filter FullyQualifiedName~CreateDefault_ExpandedCatalogHasGardenAndOverlappingZones`

Expected: FAIL because the current fixture has 3 rooms, 4 lights, and 1 zone.

- [ ] **Step 3: Expand `SimulatorFixtures.Create() -> SimulatorSnapshot`**

Add Bedroom, Hallway, and Garden groups; put exactly five uniquely named lamps in Garden; and add two uniquely named zones whose valid `LightIds` overlap room membership and span room boundaries. Preserve the current Kitchen, Lounge, Office, and Downstairs resources.

- [ ] **Step 4: Run the focused fixture tests to verify they pass**

Run: `dotnet test HuePilot.Simulator.Tests --filter FullyQualifiedName~SimulatorStateTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HuePilot.Simulator/Simulation/SimulatorFixtures.cs HuePilot.Simulator.Tests/SimulatorStateTests.cs
git commit -m "feat: expand simulator fixtures"
```

### Task 2: Verify simulator HTTP resource exposure

**Files:**
- Test: `HuePilot.Simulator.Tests/HueV2EndpointTests.cs`

**Interfaces:**
- Consumes: the fixture catalog from `SimulatorState.CreateDefault()` through `/clip/v2/resource/{resourceType}`.
- Produces: verification that the expanded fixture remains discoverable through existing endpoints.

- [ ] **Step 1: Run the simulator test project**

Run: `dotnet test HuePilot.Simulator.Tests`

Expected: PASS, including room, light, zone, and grouped-light endpoint assertions against the expanded catalog.

- [ ] **Step 2: Build the simulator project**

Run: `dotnet build HuePilot.Simulator/HuePilot.Simulator.csproj --no-restore`

Expected: Build succeeded with 0 errors.

# Simulator Dashboard Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add presentation-focused color controls and visible light state to the simulator dashboard.

**Architecture:** The existing `SimulatorState` remains the single state boundary. The Blazor page owns a fixed color-palette projection that calls `UpdateLight`; CSS renders the selected color and compact dashboard layout.

**Tech Stack:** .NET 10 Blazor server interactivity, xUnit, local CSS.

**Spec:** `docs/superpowers/specs/2026-11-06-simulator-dashboard-polish-design.md`

## Global Constraints

- Keep Hue XY values in `SimulatorState`; do not add arbitrary color conversion or external dependencies.
- Use fixed, named color swatches and a local modern font stack.
- Preserve periodic dashboard refresh for changes made through HuePilot.

## Review Focus

- A swatch must retain brightness and power while changing color; Task 1 tests this state transition.
- An unsupported XY value from the UI must remain rejected without mutation; existing state validation remains covered.
- The selected color indicator must match a known preset or safely display a neutral custom state; Task 2 handles this projection.

---

### Task 1: Add Palette State Coverage

**Files:**
- Modify: `HuePilot.Simulator.Tests/SimulatorStateTests.cs`

- [ ] Write a failing test showing that `UpdateLight` stores a valid preset XY value while retaining brightness and power.
- [ ] Run `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore --filter FullyQualifiedName~SimulatorStateTests` and verify it fails.
- [ ] Add only the state behavior needed by the test, if absent.
- [ ] Run the focused test again and verify it passes.

### Task 2: Render Compact Color Controls

**Files:**
- Modify: `HuePilot.Simulator/Components/Pages/Home.razor`
- Modify: `HuePilot.Simulator/Components/Pages/Home.razor.css`

- [ ] Define the fixed swatch palette and render per-light color buttons, brightness text, and selected-color indicator through `SimulatorState.UpdateLight`.
- [ ] Remove the hero section and apply the local modern font stack with compact card styling.
- [ ] Run `dotnet test HuePilot.Simulator.Tests/HuePilot.Simulator.Tests.csproj --no-restore` and `dotnet build HuePilot.Simulator/HuePilot.Simulator.csproj --no-restore`.

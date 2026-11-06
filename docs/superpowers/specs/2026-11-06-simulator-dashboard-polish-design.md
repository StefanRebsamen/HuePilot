# Simulator dashboard polish design

## Intent

Make the simulator dashboard presentation-friendly while exposing the simulated brightness and color state clearly.

## Goals

- Replace the text hero with a compact dashboard-only layout.
- Display every light’s on/off state, brightness percentage, and current color.
- Add a small, fixed palette of color swatches to each light card. Selecting a swatch writes its predefined Hue XY
  value through the existing `SimulatorState.UpdateLight` boundary.
- Use a modern local font stack, with no network-loaded fonts or new dependencies.

## Non-goals

- Arbitrary color picking, RGB/Hue-XY conversion, persistence, or changes to the Hue API contract.

## Approach

Two approaches were considered: native arbitrary color picking and named preset swatches. Preset swatches were
selected because they are predictable and readable on a presentation screen, avoid extra conversion logic, and
reuse the simulator’s existing XY color state.

Each card will show a color-dot/swatch and a text label. Palette choices will be fixed named constants in the page;
the active choice is determined by its XY value. The visual style will use `Inter, ui-sans-serif, system-ui,
sans-serif` with the existing local fallback path.

## Constraints

- Preserve the existing periodic refresh so changes from HuePilot remain visible.
- Validate state behavior with tests before changing the UI.
- Do not make external font requests or add packages.

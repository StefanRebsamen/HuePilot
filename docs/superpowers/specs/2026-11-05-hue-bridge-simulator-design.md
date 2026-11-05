# Hue Bridge simulator design

## Intent

Provide a self-contained, presentation-safe Hue Bridge stand-in that lets HuePilot use its production
`HueApiHueService` without physical Hue hardware. The simulator must make simulated light changes visible in a
simple browser UI.

## Goals

- Add a separate `HuePilot.Simulator` Blazor Web App project to `HuePilot.sln`.
- Serve the Hue v2 endpoints that HuePilot currently calls: discovery for lights, rooms, zones, scenes, devices,
  and grouped lights; and updates for lights, scenes, and grouped lights.
- Maintain one in-memory, deterministic resource graph that backs both the simulator UI and API.
- Include several lights spread across rooms and zones, plus scenes, with controls that show power, brightness, and
  color state.
- Allow HuePilot to use the simulator by setting the existing Bridge address to the simulator HTTPS host and any
  non-empty application key.

## Non-goals

- Emulating the complete Hue v2 API, bridge discovery, pairing, streaming events, persistence, authentication, or
  third-party client compatibility beyond HuePilot's exercised calls.
- Changing HuePilot's validation or allowing direct unrestricted API operations.

## Constraints

- API responses must use the Hue v2 `/clip/v2/resource/{type}` response envelope and shapes consumed by the
  `HueApi` package.
- The simulator runs locally over the ASP.NET Core development HTTPS endpoint; it requires no secrets or physical
  Bridge.
- API input is treated as untrusted and only supports the update bodies HuePilot emits.

## Approaches considered

1. A separate Blazor Web App with minimal API endpoints and a shared in-memory state store. This was selected: it
   cleanly represents the bridge boundary, is simple to start, and gives the presentation a live visual companion.
2. A minimal API with static HTML. It is smaller but does not meet the Blazor-app requirement as directly.
3. Hosting simulator endpoints in HuePilot. It would blur client and Bridge roles and would not be a separate
   project.

## Design

`HuePilot.Simulator` contains:

- `SimulatorState`, a singleton storing fixed GUID-based resources and their mutable on, brightness, and color
  values. It uses atomic mutations and immutable snapshots so concurrent API requests and Blazor circuits cannot
  observe partially changed state.
- Hue v2 endpoint mappings for `GET /clip/v2/resource/light`, `room`, `zone`, `scene`, `device`, and
  `grouped_light`; and `PUT /clip/v2/resource/light/{id}`, `scene/{id}`, and `grouped_light/{id}`. Every endpoint
  returns a Hue-style `data` array; updates return the updated resource identity in `data`.
- A server-interactive Blazor dashboard that renders rooms and zones with their lights and exposes presentation
  controls. It refreshes from the shared state after local actions; a short polling interval reflects changes made
  through HuePilot.
- Fixed scenes whose activation applies documented state changes to their associated lights. Room and zone state is
  derived after every mutation: a group is on when any member light is on, and its brightness is the average of its
  active lights. Overlapping zones therefore update immediately.

The response graph preserves the relationships HuePilot's mapper needs: room/zone children point to devices,
devices point to light services, and each grouped light declares the owning room or zone through its `owner` field.
All resource IDs are stable UUIDs. Hue color coordinates are retained as received and rendered with a documented
dashboard approximation.

Unknown resource IDs, malformed bodies, unsupported properties, non-finite/out-of-range values, and invalid color
coordinates are rejected without mutation using a consistent Hue-style error response.

## Validation and tests

- Unit-test the state store for power, brightness, color, grouped-light fan-out, and scene activation.
- Integration-test the supported API endpoints against a loopback test host, including Hue-style resource envelopes
  and mutation results.
- Compatibility-test all HuePilot operations through its production `HueApiHueService`: discovery, light and
  room/zone status, light and group power/brightness/color, and scene activation.
- Add a configuration/run guide explaining how to start the simulator and point HuePilot at its HTTPS URL. Confirm
  before implementation that `LocalHueApi` accepts a localhost address with a port and document the exact accepted
  `Bridge:IpAddress` format.

## Open implementation detail

The implementation will confirm the precise serialised response fields expected by the installed `HueApi` client
and will use a compatibility test that drives HuePilot's existing `HueApiHueService` against the simulator.

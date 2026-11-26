# Group-owned Hue resources — design

## Intent

Make the application catalog reflect the useful domain relationship: rooms and zones own the lights and scenes they contain. Every resource remains identified by its immutable Hue resource ID, never solely by name.

## Goals

- `HueResourceGroup` exposes its member resources, including stable IDs.
- Both lights and scenes appear in their associated room or zone.
- The application can still find any resource globally by its ID for validation and execution.
- Simplify membership mapping and consumers by removing resource-owned group collections.

## Non-goals

- No change to Bridge operations, persistence, authentication, or AI-provider integration.
- No inference of a missing or ambiguous Hue association.

## Approaches

1. **Flat catalog plus group-owned memberships (selected).** Keep a flat resource catalog for safe global ID lookup, and put resource membership on each room/zone group. This gives a natural browse structure without weakening validation.
2. **Groups-only hierarchy.** Rejected because global resource lookup and validation would require flattening repeatedly.

## Design

- `HueResource` remains a compact immutable `(Id, Name, Type)` value.
- `HueResourceGroup` represents a room or zone and adds `Resources`, containing the associated light and scene resources.
- The mapper builds one canonical resource for every Hue light, room, zone, and scene, then constructs groups from the canonical resources. It maps room/zone child devices to light service IDs, and scene group IDs to their owning room or zone.
- Consumers obtain a resource's groups by querying groups whose `Resources` contain its ID. Search results retain explicit group IDs/names/types so the AI may safely disambiguate and the UI can display associations.
- Unknown IDs or unmatched associations are omitted; no associations are synthesized.

## Verification

- Mapper tests prove a room/zone contains member lights by ID and the owning group contains its scene.
- Intent-parser tests cover filtering and validating a resource against a group-owned membership.
- Run the relevant test project and solution build.

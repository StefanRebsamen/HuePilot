# Simulator fixture expansion design

## Intent

Provide a richer, static Hue Bridge Simulator catalog for learning and demo use, without changing simulator behavior or adding persistence.

## Goals

- Retain the existing Kitchen, Lounge, Office, and Downstairs resources.
- Add two indoor rooms and a Garden room with five garden lamps.
- Add two zones, with at least one spanning lights from more than one room.
- Keep all resource IDs deterministic and all group memberships valid.
- Strengthen fixture tests to make the expanded catalog and zone ownership explicit.

## Non-goals

- No UI, endpoint, state-management, persistence, or real Hue Bridge changes.
- No new scene requirement.

## Approaches

1. Add a balanced set of rooms, lamps, and zones with overlapping zone membership. Chosen: it expands normal and cross-room use cases while keeping the fixture readable.
2. Add a much larger whole-home catalog. Rejected: it adds maintenance without improving the simulator's core coverage proportionally.
3. Add only isolated rooms and zones. Rejected: it would not exercise zones that span room boundaries.

## Design

The fixture will add Bedroom, Hallway, and Garden rooms. Garden contains five individually named lamps. Two new zones will demonstrate cross-room grouping; the existing Downstairs zone remains. Tests will assert catalog counts, garden membership, and that every group references known lights.

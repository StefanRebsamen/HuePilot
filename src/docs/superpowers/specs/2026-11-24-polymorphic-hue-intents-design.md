# Polymorphic Hue Intents Design

## Intent

Replace the operation-dependent `HueIntent` record with concrete intent types so each Hue operation carries only the data it needs.

## Goals

- Keep AI output untrusted and retain existing resource-resolution and validation safeguards.
- Represent brightness, color, queries, clarification, and action operations with focused types.
- Preserve the current parser and conversation behavior.

## Non-goals

- Change supported Hue operations, provider contracts, persistence, or execution behavior.

## Approaches

1. Polymorphic intent hierarchy (selected): shared target/group data in a base type; one subtype per operation family. It prevents invalid operation-specific argument combinations in application code.
2. Envelope with nested payload: retains a wide wrapper and therefore does less to simplify consumers.
3. Nested optional argument object: minimizes churn but continues to represent invalid combinations.

## Design

`HueIntent` becomes an abstract base record containing only common target and group-disambiguation fields. Concrete records represent clarification, resource listing, status lookup, on/off actions, brightness, color, and scene activation; their CLR type is the operation discriminator.

Structured-provider JSON is deserialized using an `operation` discriminator into the appropriate subtype. Tool-calling parser proposals are mapped to concrete types after the discovered resource has been resolved. Conversation planning and action validation use type dispatch, while the existing checks for target types, brightness ranges, and supported colors remain authoritative.

## Constraints

- Do not expose Hue credentials or AI keys.
- Validate every model-supplied target against discovered Bridge resources.
- Tests must not require a live Hue Bridge or AI provider.

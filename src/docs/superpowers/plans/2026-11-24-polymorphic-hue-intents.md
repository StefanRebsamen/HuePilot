# Polymorphic Hue Intents Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Model every supported Hue intent as a focused concrete type rather than an operation-dependent record.

**Architecture:** Introduce an abstract `HueIntent` base and concrete operation/query/clarification records in the Hue models boundary. Map provider payloads to those types, then use pattern matching in the conversation and validation layers while retaining all current discovery and input validation.

**Tech Stack:** .NET 10, C#, xUnit, System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-11-24-polymorphic-hue-intents-design.md`

## Global Constraints

- AI-produced targets remain untrusted and must be resolved against discovered Hue resources.
- No live Hue Bridge or AI provider is required by tests.
- Supported operations and user-visible safety behavior remain unchanged.

## Review Focus

- A brightness intent rejects values outside 1–100.
- A color intent rejects an absent or unsupported color.
- A clarification intent never reaches action execution.
- Provider JSON maps each operation discriminator to the right intent subtype.
- Scene activation remains exclusive to scenes.

---

### Task 1: Intent model and validator

**Files:**
- Modify: `HuePilot/Application/Hue/Models/HueIntent.cs`
- Modify: `HuePilot/Application/Hue/HueActionValidator.cs`
- Modify: `HuePilot.Tests/HueActionValidatorTests.cs`

**Interfaces:**
- Produces: abstract `HueIntent`, concrete operation/query/clarification records, and `HueActionValidator.TryCreate(HueIntent, HueResource, out HueAction?, out string)`.

- [ ] Write failing validator tests using `SetBrightnessIntent` and `SetColorIntent`.
- [ ] Run `dotnet test HuePilot.Tests --filter FullyQualifiedName~HueActionValidatorTests` and observe compilation failure until the focused types exist.
- [ ] Implement the hierarchy and type-dispatched validator.
- [ ] Rerun the focused tests and confirm they pass.

### Task 2: Parser and conversation migration

**Files:**
- Modify: `HuePilot/Application/IntentParsing/OpenAiStructuredIntentParser.cs`
- Modify: `HuePilot/Application/IntentParsing/OpenAiToolCallingIntentParser.cs`
- Modify: `HuePilot/Application/Hue/HueConversationService.cs`
- Modify: `HuePilot.Tests/OpenAiToolCallingIntentParserTests.cs`
- Modify: `HuePilot.Tests/HueConversationServiceTests.cs`

**Interfaces:**
- Consumes: concrete intent types from Task 1.
- Produces: parser output and conversation plans equivalent to the previous behavior.

- [ ] Write failing tests for operation-discriminated intent parsing and concrete intents through conversation planning.
- [ ] Run the affected tests and observe the expected failure.
- [ ] Map provider proposals and JSON to concrete records; use type dispatch in conversation planning.
- [ ] Rerun affected tests and confirm they pass.

### Task 3: Complete migration and verification

**Files:**
- Modify: all remaining `HueIntent` construction sites in application, MCP, and test projects.

**Interfaces:**
- Consumes: focused intent hierarchy from Tasks 1–2.
- Produces: a compiling solution with no wide-record construction remaining.

- [ ] Run `rg -n 'new HueIntent'` and replace remaining construction sites with concrete records.
- [ ] Run `dotnet test HuePilot.sln` and `dotnet build HuePilot.sln`.
- [ ] Confirm the full suite and build pass.

# Group-owned Hue Resources Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make rooms and zones own their associated light and scene resources while preserving global ID-based validation.

**Architecture:** Retain the existing flat `IReadOnlyList<HueResource>` returned by `IHueService`, but remove reverse group membership from a resource. `HueResourceGroup` will own a `Resources` collection with canonical light and scene resource objects. Consumers derive a resource's matching groups from the catalog before displaying or validating an association.

**Tech Stack:** .NET 10, C#, xUnit, Blazor Server, Hue API client.

**Spec:** `docs/superpowers/specs/2026-11-25-group-owned-hue-resources-design.md`

## Global Constraints

- Hue IDs are opaque stable identifiers and must be used for validation and resource membership.
- Keep Bridge credentials and AI-provider keys server-side; no new persistence, authentication, or remote access.
- A missing or ambiguous Hue association must not be inferred.
- Tests must run without a physical Hue Bridge or live AI service.

## Review Focus

- A light linked through a Hue device child appears in every room/zone that contains that device.
- A scene appears only in its explicit owning room/zone, not all groups with a matching name.
- A resource with no group association cannot pass a requested group-ID validation.
- Duplicate resource names remain disambiguated by exact resource and group IDs.
- Resource browsing reports unassociated lights/scenes without throwing on empty group memberships.

---

### Task 1: Model and catalog mapping

**Files:**
- Modify: `HuePilot/Application/Hue/Models/HueResource.cs`
- Modify: `HuePilot/Application/Hue/Models/HueResourceGroup.cs`
- Modify: `HuePilot/Application/Hue/HueResourceMapper.cs`
- Test: `HuePilot.Tests/HueResourceMapperTests.cs`

**Interfaces:**
- Produces: `HueResource(string Id, string Name, HueResourceType Type)`.
- Produces: `HueResourceGroup(string Id, string Name, HueResourceType Type, IReadOnlyList<HueResource> Resources)`.

- [ ] **Step 1: Write failing mapper tests**

Add tests proving a room and a zone contain the same canonical light by its exact ID, and that the room owning a scene contains that exact scene ID.

- [ ] **Step 2: Run mapper tests to verify they fail**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~HueResourceMapperTests`

Expected: FAIL because group resources do not exist yet.

- [ ] **Step 3: Implement group-owned membership**

Build canonical resource objects first. Change `HueResourceGroup` to hold `Resources`; map device/light children to member light IDs and a scene's group ID to a member scene ID. Do not create memberships for unrecognized IDs.

- [ ] **Step 4: Run mapper tests to verify they pass**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~HueResourceMapperTests`

Expected: PASS.

### Task 2: ID-safe lookup and proposal validation

**Files:**
- Modify: `HuePilot/Application/IntentParsing/OpenAiToolCallingIntentParser.cs`
- Modify: `HuePilot/Application/IntentParsing/OpenAiStructuredIntentParser.cs`
- Test: `HuePilot.Tests/OpenAiToolCallingIntentParserTests.cs`
- Test: `HuePilot.Tests/OpenAiIntentParserBaseTests.cs`

**Interfaces:**
- Consumes: group resources from Task 1.
- Produces: lookup results with each matched resource's containing groups and accepts `targetGroupId` only when the exact resource ID is a group member.

- [ ] **Step 1: Write failing parser tests**

Use group-owned fixture data. Assert a light lookup scoped by a room returns its exact light ID and group metadata; assert proposal validation rejects an unrelated group ID.

- [ ] **Step 2: Run parser tests to verify they fail**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~OpenAiToolCallingIntentParserTests`

Expected: FAIL because lookup and validation still read resource-owned groups.

- [ ] **Step 3: Implement catalog group queries**

Add a focused private helper that derives containing groups by exact resource ID. Use it for structured catalog serialization, tool lookup filtering/output, and proposed-action group validation.

- [ ] **Step 4: Run parser tests to verify they pass**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~OpenAiToolCallingIntentParserTests`

Expected: PASS.

### Task 3: Browse UI and remaining catalog consumers

**Files:**
- Modify: `HuePilot/Components/Pages/Resources.razor`
- Modify: `HuePilot/Application/Hue/DemoHueService.cs`
- Modify: `HuePilot.Tests/HueConversationServiceTests.cs`
- Modify: other affected Hue catalog fixtures under `HuePilot.Tests/`

**Interfaces:**
- Consumes: the group-owned catalog defined in Task 1.
- Produces: resource browsing that shows containing rooms/zones derived from group membership.

- [ ] **Step 1: Update affected test fixtures and write a failing consumer test if an observable behavior is not covered**

Convert group memberships in fakes to group-owned `Resources`; add a narrow behavior assertion only where existing tests do not cover a group-scoped resource query.

- [ ] **Step 2: Run affected tests to verify they fail for the old model**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter "FullyQualifiedName~HueConversationServiceTests|FullyQualifiedName~OpenAiIntentParserBaseTests"`

Expected: compilation failure or assertion failure until consumers stop reading `HueResource.Groups`.

- [ ] **Step 3: Update consumers and UI**

Construct demo groups with member resources, derive display associations from the group catalog, and remove obsolete group-label helpers if no longer needed.

- [ ] **Step 4: Run focused tests to verify they pass**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter "FullyQualifiedName~HueConversationServiceTests|FullyQualifiedName~OpenAiIntentParserBaseTests"`

Expected: PASS.

### Task 4: End-to-end verification

**Files:**
- Verify only.

- [ ] **Step 1: Run the test suite**

Run: `dotnet test HuePilot.sln`

Expected: PASS with zero failures.

- [ ] **Step 2: Build the solution**

Run: `dotnet build HuePilot.sln --no-restore`

Expected: PASS with zero errors.

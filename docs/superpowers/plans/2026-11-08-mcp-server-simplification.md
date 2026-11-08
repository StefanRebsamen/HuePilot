# MCP Server Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep only the local HTTP MCP server, retain safe development payload logging, and move MCP tests to a dedicated test project.

**Architecture:** `HuePilot.Mcp` starts one `WebApplication` with HTTP MCP mapped at `/mcp`. Its payload middleware remains a bounded streaming logger with minimal JSON field filtering. `HuePilot.Mcp.Tests` becomes the sole home for MCP tool and middleware tests and is added to the solution.

**Tech Stack:** .NET 10, ASP.NET Core, ModelContextProtocol, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-08-mcp-server-simplification-design.md`

## Global Constraints

- Retain only `http://127.0.0.1:5179/mcp` as the MCP transport.
- Keep logging Development-only, bounded, and emitted via `ILogger`.
- Do not log headers or secret-bearing JSON values.
- Tests require no physical Hue Bridge or live MCP service.

## Review Focus

- Starting the server must not accept or need a transport command-line switch.
- Development logging must still preserve request and response stream behavior.
- Production environments must not enable payload logging.
- The new test project must compile and run independently from `HuePilot.Tests`.
- No MCP test source or project reference may remain in `HuePilot.Tests`.

### Task 1: Create the dedicated MCP test project

**Files:**
- Create: `HuePilot.Mcp.Tests/HuePilot.Mcp.Tests.csproj`
- Create: `HuePilot.Mcp.Tests/HueMcpToolsTests.cs`
- Create: `HuePilot.Mcp.Tests/McpPayloadLoggingMiddlewareTests.cs`
- Modify: `HuePilot.Tests/HuePilot.Tests.csproj`
- Delete: `HuePilot.Tests/HueMcpToolsTests.cs`
- Delete: `HuePilot.Tests/McpPayloadLoggingMiddlewareTests.cs`
- Modify: `HuePilot.sln`

**Interfaces:**
- Consumes: public MCP tools and payload middleware from `HuePilot.Mcp`.
- Produces: an independently runnable xUnit MCP test assembly.

- [ ] **Step 1: Add a failing project-isolation check**

Create the new test project and a representative MCP test, then remove the MCP project reference from `HuePilot.Tests`. Run the new project before moving implementation tests to establish the missing test source failure.

- [ ] **Step 2: Move MCP test sources and add the project to the solution**

Move both existing MCP test files unchanged into `HuePilot.Mcp.Tests`, preserve required package references and global xUnit using, remove their originals and the obsolete `HuePilot.Mcp` reference from `HuePilot.Tests`, and add the new project to `HuePilot.sln`.

- [ ] **Step 3: Verify project isolation**

Run: `dotnet test HuePilot.Mcp.Tests/HuePilot.Mcp.Tests.csproj`

Expected: PASS.

### Task 2: Remove stdio mode and simplify HTTP startup

**Files:**
- Modify: `HuePilot.Mcp/Program.cs`
- Modify: `HuePilot.Mcp.Tests/McpPayloadLoggingMiddlewareTests.cs`

**Interfaces:**
- Consumes: `McpPayloadLoggingApplicationBuilderExtensions.UseMcpPayloadLogging`.
- Produces: one HTTP-only server startup path with no stdio host or command-line transport choice.

- [ ] **Step 1: Write a failing host-startup behavior test**

Add a test seam that proves HTTP startup is configured without a mode argument and retains development-only middleware registration.

- [ ] **Step 2: Simplify `Program.cs`**

Remove the stdio host method, the `--http` constant and mode parsing, and call the HTTP host startup directly. Preserve the loopback binding, MCP mapping, service registration, and development-only logger middleware.

- [ ] **Step 3: Verify dedicated MCP tests**

Run: `dotnet test HuePilot.Mcp.Tests/HuePilot.Mcp.Tests.csproj`

Expected: PASS.

### Task 3: Verify the solution

**Files:**
- Modify: none expected.

- [ ] **Step 1: Run all tests**

Run: `dotnet test HuePilot.sln`

Expected: PASS.

- [ ] **Step 2: Build the solution**

Run: `dotnet build HuePilot.sln --no-restore`

Expected: PASS with no warnings or errors.

- [ ] **Step 3: Inspect the change**

Run: `git diff --check && git status --short`

Expected: no whitespace errors; only planned MCP-server simplification and test-project changes alongside the existing user edits.

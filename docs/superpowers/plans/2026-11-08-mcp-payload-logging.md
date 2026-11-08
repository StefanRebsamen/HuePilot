# MCP Payload Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Log redacted HTTP MCP request and response payloads through `ILogger` during local development without changing MCP behavior.

**Architecture:** A focused ASP.NET Core middleware uses bounded tee streams only for `/mcp`, forwards request and response bytes directly to preserve streaming behavior, and logs redacted JSON through an injected logger. The MCP HTTP host conditionally adds it only in the Development environment; stdio remains untouched.

**Tech Stack:** .NET 10, ASP.NET Core middleware, `Microsoft.Extensions.Logging`, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-08-mcp-payload-logging-design.md`

## Global Constraints

- Log via `ILogger`; do not write directly to the console or persist payloads.
- Restrict body logging to the `/mcp` HTTP path and the Development environment.
- Redact secret-bearing JSON fields and log no headers.
- Preserve request and response content for the MCP endpoint.
- Do not modify stdio MCP transport or Hue operation validation.

## Review Focus

- A nested, case-insensitive secret property must have its value redacted before it reaches the logger.
- A request exceeding the configured cap must not cause unbounded memory use or expose its full payload.
- Non-MCP paths must not be buffered or logged.
- The caller must receive exactly the response written by the downstream MCP endpoint.
- A downstream exception must retain normal propagation and must not replace the caller's response with logging output.

### Task 1: Implement and test payload-capture middleware

**Files:**
- Create: `HuePilot.Mcp/McpPayloadLoggingMiddleware.cs`
- Create: `HuePilot.Tests/McpPayloadLoggingMiddlewareTests.cs`

**Interfaces:**
- Produces: `public sealed class McpPayloadLoggingMiddleware` with `Task InvokeAsync(HttpContext context)`.
- Produces: a small options type carrying the maximum captured body size.
- Consumes: `RequestDelegate` and `ILogger<McpPayloadLoggingMiddleware>` through constructor injection.

- [ ] **Step 1: Write failing middleware tests**

Add tests that invoke the middleware with an in-memory `DefaultHttpContext` and capture logger. Assert it logs a `/mcp` JSON request and response, redacts nested case-insensitive sensitive fields, skips a non-`/mcp` request, preserves downstream response content, and records a truncation marker for a body above the configured limit.

- [ ] **Step 2: Run the middleware tests to verify they fail**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~McpPayloadLoggingMiddlewareTests`

Expected: FAIL because `McpPayloadLoggingMiddleware` does not exist.

- [ ] **Step 3: Implement `McpPayloadLoggingMiddleware` in `HuePilot.Mcp/McpPayloadLoggingMiddleware.cs`**

Implement the public middleware and bounded capture options. For `/mcp`, tee request and response bytes within the configured capture size, redact recursively by property name, restore the original streams after invocation, and emit payload entries with the typed `ILogger`. Delegate directly for other paths.

- [ ] **Step 4: Run the middleware tests to verify they pass**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~McpPayloadLoggingMiddlewareTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HuePilot.Mcp/McpPayloadLoggingMiddleware.cs HuePilot.Tests/McpPayloadLoggingMiddlewareTests.cs
git commit -m "feat: log redacted MCP payloads"
```

### Task 2: Enable middleware in the development HTTP host

**Files:**
- Modify: `HuePilot.Mcp/Program.cs:16-31`
- Modify: `HuePilot.Tests/McpPayloadLoggingMiddlewareTests.cs`

**Interfaces:**
- Consumes: `McpPayloadLoggingMiddleware` from Task 1 via the ASP.NET Core middleware pipeline.
- Produces: development-only registration before `MapMcp("/mcp")` handling.

- [ ] **Step 1: Write a failing host-wiring test**

Add a testable registration helper, or an equivalent seam, and test that development configuration adds the middleware while production configuration does not.

- [ ] **Step 2: Run the host-wiring test to verify it fails**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~McpPayloadLoggingMiddlewareTests`

Expected: FAIL because host registration does not yet conditionally add the middleware.

- [ ] **Step 3: Update `RunHttpModeAsync` in `HuePilot.Mcp/Program.cs`**

Register and place the payload middleware only when `app.Environment.IsDevelopment()` is true, before the mapped MCP endpoint. Do not change stdio logging or transport setup.

- [ ] **Step 4: Run the middleware tests to verify they pass**

Run: `dotnet test HuePilot.Tests/HuePilot.Tests.csproj --filter FullyQualifiedName~McpPayloadLoggingMiddlewareTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add HuePilot.Mcp/Program.cs HuePilot.Tests/McpPayloadLoggingMiddlewareTests.cs
git commit -m "feat: enable MCP payload logging in development"
```

### Task 3: Verify the complete solution

**Files:**
- Modify: none expected.

**Interfaces:**
- Consumes: the complete Task 1 and Task 2 implementation.
- Produces: verified build and test evidence.

- [ ] **Step 1: Run the test suite**

Run: `dotnet test HuePilot.sln`

Expected: PASS with no failures.

- [ ] **Step 2: Run the solution build**

Run: `dotnet build HuePilot.sln --no-restore`

Expected: PASS with no compilation errors.

- [ ] **Step 3: Inspect the diff**

Run: `git diff --check && git diff -- HuePilot.Mcp/Program.cs HuePilot.Mcp/McpPayloadLoggingMiddleware.cs HuePilot.Tests/McpPayloadLoggingMiddlewareTests.cs`

Expected: no whitespace errors; only planned source and test changes.

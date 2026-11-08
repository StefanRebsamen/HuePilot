# MCP Server Simplification Design

## Intent

Reduce the MCP demo server to its local HTTP transport, keep payload troubleshooting available through `ILogger`, and isolate MCP tests in their own project.

## Goals

- Remove stdio transport and command-line mode selection.
- Retain local HTTP MCP at `http://127.0.0.1:5179/mcp`.
- Keep payload logging small and bounded, writing the raw payload text for this demo project.
- Move MCP-specific tests into `HuePilot.Mcp.Tests`.

## Non-goals

- New transports, persistence, test infrastructure shared outside MCP, or changes to Hue validation.

## Approach

Use a single `WebApplication` startup path in `Program.cs`, configure the existing MCP HTTP transport, and map `/mcp`. Keep the middleware as a local, development-only `ILogger` component that writes bounded raw payload text. Create a focused xUnit project referencing `HuePilot.Mcp`, remove the MCP project reference and moved test file from `HuePilot.Tests`, and add the new project to the solution.

## Constraints

- Do not log headers.
- Retain bounded streaming capture and Development-only activation.
- Tests must not require a Hue Bridge or live MCP client.

# MCP Payload Logging Design

## Intent

Make local MCP troubleshooting practical by logging the request and response payloads for the HTTP `/mcp` endpoint through the application's `ILogger` pipeline. The log content must not disclose credentials or other secrets.

## Goals

- Capture JSON request and response bodies for HTTP MCP traffic.
- Redact values associated with sensitive JSON property names.
- Preserve the request and response bodies so MCP processing is unchanged.
- Enable the behavior only during development, while leaving log-provider selection to standard ASP.NET Core configuration.

## Non-goals

- Persisting request or response logs.
- Logging headers or changing stdio MCP behavior.
- Altering MCP protocol handling or Hue validation.

## Constraints

- Bridge credentials and other secret values must never be written to logs.
- Logging is restricted to `/mcp` and bounded by a configurable maximum payload size.
- The middleware uses `ILogger`; it does not write directly to any provider such as the console.

## Approaches

1. A development-only custom ASP.NET Core middleware that buffers bodies, redacts JSON, and emits `ILogger` entries. Chosen because it provides precise endpoint scoping and redaction.
2. Built-in HTTP logging. Rejected because its body handling and redaction controls are too coarse for MCP payload visibility.
3. Structured tool-call logging. Rejected because it does not expose the actual MCP request and response payloads needed for protocol troubleshooting.

## Design

Add `McpPayloadLoggingMiddleware` in `HuePilot.Mcp`. It runs only for `/mcp`; for all other paths it immediately delegates. For MCP traffic, bounded tee streams observe bytes as the downstream endpoint reads the request and writes the response, forwarding those bytes immediately without retaining the full body. It logs request and response JSON through an injected `ILogger<McpPayloadLoggingMiddleware>` after recursively replacing values under sensitive property names such as `authorization`, `token`, `key`, `secret`, `password`, and `cookie`.

The HTTP host registers the middleware only when `IHostEnvironment.IsDevelopment()` is true. A small options type supplies a maximum body length, so unexpectedly large or non-JSON payloads are represented safely rather than fully emitted. The stdio host remains unchanged.

Tests will exercise the middleware directly with an in-memory ASP.NET Core context and a capture logger. They will prove endpoint filtering, request and response capture, JSON redaction, size handling, and transparent forwarding of the response body.

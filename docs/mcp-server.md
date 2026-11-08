# HuePilot MCP server

HuePilot includes an MCP server in `src/HuePilot.Mcp`. It reuses the Hue Bridge v2 adapter and the app's action validator. It requires the Bridge IP and application key already stored in HuePilot's .NET user secrets; it fails closed when they are missing. It never accepts credentials or arbitrary URLs as tool arguments.

## Tools

- `list_hue_resources` — read-only list of discovered rooms, zones, lights, and scenes, including IDs and associated room/zone groups.
- `get_hue_status` — read-only structured on/off, brightness, and available xy color state for a discovered light, room, or zone.
- `set_hue_power` — turn a discovered light, room, or zone on/off.
- `set_hue_brightness` — set a discovered light, room, or zone to 1–100 percent.
- `set_hue_color` — set a discovered light, room, or zone to an allowlisted named color.
- `activate_hue_scene` — activate a discovered existing scene on its associated group; optionally pass a room ID to verify the intended room.

Mutation tools verify the supplied ID against a fresh Bridge resource list, validate the resource type and arguments, and then go through `IHueService`. They do not accept Bridge URLs, raw API bodies, or credentials. This is a local personal server, not a multi-user authorization boundary. Do not expose it over a network or add more users without designing authentication and per-user authorization.

## OpenCode

Project-local OpenCode configuration is in `.opencode/opencode.json`. The recommended macOS setup uses Streamable HTTP bound exclusively to `127.0.0.1:5179`: start the server from Terminal, where HuePilot has local-network access, then OpenCode connects to that loopback endpoint. This keeps Bridge networking in the Terminal-launched .NET process rather than OpenCode's managed stdio child. Resource listing is allowed; each mutation tool is configured to require OpenCode permission approval.

Start the HTTP MCP server from Terminal at the HuePilot project root and leave it running:

```sh
dotnet run --project src/HuePilot.Mcp/HuePilot.Mcp.csproj
```

The server binds only to `127.0.0.1` and serves `/mcp`. OpenCode connects using the project config. Stop the server with Ctrl+C.

Check the connection with:

```sh
opencode mcp list
```

The agent can list resources first, then use a returned ID with an appropriate control tool. OpenCode may present permission approval before each mutation.

## Tests

The dedicated MCP test project uses a fake Hue service and never require a Bridge:

```sh
dotnet test src/HuePilot.Mcp.Tests/HuePilot.Mcp.Tests.csproj
```

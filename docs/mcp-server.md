# HuePilot MCP server

HuePilot includes an MCP server in `src/HuePilot.Mcp`. It reuses the Hue Bridge v2 adapter and the app's action validator. It requires the Bridge IP and application key already stored in HuePilot's .NET user secrets; it fails closed when they are missing. It never accepts credentials or arbitrary URLs as tool arguments.

## Tools

- `list_hue_resources` — read-only list of discovered rooms, lights, and scenes, including IDs and associated room/zone groups.
- `set_hue_power` — turn a discovered light or room on/off.
- `set_hue_brightness` — set a discovered light or room to 1–100 percent.
- `set_hue_color` — set a discovered light or room to an allowlisted named color.
- `activate_hue_scene` — activate a discovered existing scene on its associated group; optionally pass a room ID to verify the intended room.

Mutation tools verify the supplied ID against a fresh Bridge resource list, validate the resource type and arguments, and then go through `IHueService`. They do not accept Bridge URLs, raw API bodies, or credentials. This is a local personal server, not a multi-user authorization boundary. Do not expose it over a network or add more users without designing authentication and per-user authorization.

## OpenCode

Project-local OpenCode configuration is in `.opencode/opencode.json`. The recommended macOS setup uses Streamable HTTP bound exclusively to `127.0.0.1:5179`: start the server from Terminal, where HuePilot has local-network access, then OpenCode connects to that loopback endpoint. This keeps Bridge networking in the Terminal-launched .NET process rather than OpenCode's managed stdio child. Resource listing is allowed; each mutation tool is configured to require OpenCode permission approval.

Start the HTTP MCP server from Terminal at the HuePilot project root and leave it running:

```sh
dotnet run --project src/HuePilot.Mcp/HuePilot.Mcp.csproj -- --http
```

The server binds only to `127.0.0.1` and serves `/mcp`. OpenCode connects using the project config. Stop the server with Ctrl+C.

Check the connection with:

```sh
opencode mcp list
```

The agent can list resources first, then use a returned ID with an appropriate control tool. OpenCode may present permission approval before each mutation.

If stdio MCP reports `NoRouteToHost` while the web app works from Terminal, use the loopback HTTP mode above: launch the .NET MCP server directly from Terminal (the process context that has Hue network access), and have OpenCode connect over `127.0.0.1`. OpenCode's own network permission does not necessarily grant the separately spawned .NET child process access to the LAN. Also verify that `Bridge:IpAddress` matches the Hue app/router; discovery only reports an address and does not confirm API reachability. Update only the IP secret if it changed; the application key secret does not need to be re-entered.

## Other MCP clients

The server still supports stdio for clients that can launch a .NET child process with local Bridge network access: run `bash src/HuePilot.Mcp/run-stdio.sh` from the repository root. It uses the HuePilot web project's user-secrets store.

## Tests

The MCP tool unit tests use a fake Hue service and never require a Bridge:

```sh
dotnet test src/HuePilot.sln
```

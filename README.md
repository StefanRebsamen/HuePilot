# HuePilot

HuePilot is a .NET 10 Blazor learning project for AI-assisted development and in-app AI. It provides a chat-first interface for Philips Hue, a resources browser, and a local MCP server for compatible agents.

## Repository layout

```text
src/HuePilot/               Blazor application and Hue/OpenAI integrations
src/HuePilot.Mcp/            MCP tools for Hue resources and controls
src/HuePilot.Tests/          Offline unit tests with fakes
docs/                       Requirements, architecture decisions, and guides
src/HuePilot.sln             All .NET projects
```

## Architecture

```mermaid
flowchart LR
    subgraph Web["HuePilot Blazor Web App"]
        Chat["Chat UI"] --> Conversation["HueConversationService<br/>Plan → validate → display → execute"]
        Resources["Resources page"] --> HuePort
        Conversation --> Parser{"IIntentParser"}
        Parser --> Structured["Structured JSON parser"]
        Parser --> ToolPlanner["Bounded tool-calling planner"]
        Structured <--> OpenAI["OpenAI API"]
        ToolPlanner <--> OpenAI
        Conversation --> HuePort["IHueService"]
    end

    subgraph AgentHost["Agent host"]
        Agent["OpenCode agent"] <--> MCP["HuePilot MCP<br/>loopback HTTP or stdio"]
        MCP --> MCPTools["List, validate, and control tools"]
        MCPTools --> MCPHuePort["IHueService"]
    end

    subgraph HueIntegration["Hue integration code (each host has its own instance)"]
        HuePort --> Adapter{"Configured adapter"}
        MCPHuePort --> Adapter
        Adapter --> Live["HueApiHueService"]
        Adapter --> Demo["DemoHueService"]
    end

    Live --> Bridge["Philips Hue Bridge API v2"]

    subgraph Verification["Offline verification"]
        Unit["xUnit tests"] --> Fake["Fake Hue service"]
        Evaluation["Optional OpenAI evaluation"] --> Fake
    end
```

The model proposes; HuePilot resolves discovered resources and validates every action before execution. One request may contain multiple actions, which are executed sequentially after validation. The chat can switch between structured JSON output and tool calling, and optionally displays the model exchange in expandable technical details.

Resource records use the application-owned `HueResourceTypes` constants for canonical values such as `room`, `zone`, `light`, and `scene`. AI-produced intent type fields remain strings at the provider boundary because they are untrusted input; HuePilot resolves and validates them against the discovered resource catalog before acting.

HuePilot also reports current light and room status. The MCP server exposes the same bounded Hue service to external agents, with OpenCode approval required for mutations.

## Run the app

```sh
dotnet run --project src/HuePilot/HuePilot.csproj
```

Visit `/setup` to discover and pair a Hue Bridge. Save the IP address and generated application key using the `dotnet user-secrets` commands shown on that page. Add the OpenAI key with:

```sh
dotnet user-secrets set "OpenAI:ApiKey" "<your-key>" --project src/HuePilot/HuePilot.csproj
```

Without Bridge secrets, the app uses clearly labeled demo resources. Browse rooms, lights, scenes, and their room/zone associations at `/resources`.

In the chat, use the **AI approach** selector to switch between structured output and tool calling. The choice is persisted in the browser and restored on reload. The initial approach when no choice has been saved is configured by `AI:Approach`; optional conversation logging can be enabled with `AI:LogConversation=true` and appears in expandable technical details.

## Presentation simulator

When a physical Bridge is unavailable, start the local Hue v2 simulator in a second terminal:

```sh
dotnet run --project src/HuePilot.Simulator/HuePilot.Simulator.csproj
```

Open the HTTPS URL printed by ASP.NET Core to see the simulated rooms, zone, lights, and scene. To run HuePilot against it, set `Bridge:IpAddress` to the simulator host and HTTPS port (for example, `localhost:7242`) and set `Bridge:ApplicationKey` to any non-empty local value through user secrets. The simulator is in-memory and resets whenever it restarts.

## MCP server for OpenCode

On macOS, start the loopback-only HTTP MCP server from Terminal so the .NET process has Hue LAN access:

```sh
dotnet run --project src/HuePilot.Mcp/HuePilot.Mcp.csproj -- --http
```

Leave it running while using OpenCode. Project-local configuration is in `.opencode/opencode.json`; write tools require approval. See [`docs/mcp-server.md`](docs/mcp-server.md).

## Build and test

```sh
dotnet build src/HuePilot.sln
dotnet test src/HuePilot.sln
```

The regular test suite uses fakes and does not require a Bridge or live AI service. For optional live-model evaluation (OpenAI usage may incur charges):

```sh
dotnet run --project src/HuePilot.Eval/HuePilot.Eval.csproj -- structured
dotnet run --project src/HuePilot.Eval/HuePilot.Eval.csproj -- tools
```

## Guides

- [Requirements](docs/requirements.md)
- [Architecture decision](docs/decisions/0001-initial-architecture.md)
- [AI approach comparison](docs/ai-approaches.md)
- [AI evaluation](docs/ai-evaluation.md)
- [MCP server](docs/mcp-server.md)

Keep Bridge and OpenAI credentials in local user secrets or environment configuration. Never commit credentials or expose the Hue Bridge API key to browser code or AI tools.

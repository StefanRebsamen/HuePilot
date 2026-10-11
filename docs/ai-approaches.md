# Comparing AI integration approaches

## 1. Structured intent parsing — current implementation

The model receives the user request, recent conversation, and a catalog of Hue resource names/types. It returns one JSON intent. HuePilot resolves that name to a discovered resource, validates the action and arguments, then executes through `IHueService`. Run its evaluation with the `structured` argument.

**Strengths:** straightforward flow; one model response; easy to inspect, test, and constrain; clear split between probabilistic interpretation and deterministic execution.

**Tradeoffs:** the model is given a snapshot of the catalog up front; it cannot decide to make another read or sequence several operations. The orchestration is mostly application-driven rather than agent-driven.

## 2. Model tool/function calling — planned comparison

The application provides typed planning tools: `lookup_resources` and `propose_hue_action`. The model chooses a tool and arguments in a bounded loop. In this prototype, the second tool creates a proposal only; HuePilot validates it, displays the interpretation, then executes it through the existing service boundary. Run its evaluation with the `tools` argument.

**Strengths:** closer to an agentic loop; resources can be looked up when needed; tool schemas make available actions explicit; can support multi-step work and explain actual tool results.

**Tradeoffs:** requires a loop, tool-call/result handling, and careful limits on calls/turns; can have more latency and token cost; the model may make unnecessary calls. Tool schemas do not replace application-side validation.

**Guardrails for HuePilot:** expose only the allowlisted Hue operations; resolve names to IDs in application code; re-check the resource and capability on every mutation; reject malformed arguments; cap the loop; keep credentials and raw Hue networking inside the server; test with a fake Hue service.

## 3. MCP — a complementary protocol

MCP standardizes how a client discovers and invokes tools/resources exposed by a server. It is not itself a planning strategy and does not replace either structured intent or tool calling. A future HuePilot MCP server could reuse the same application layer so local agents can access Hue capabilities. Keep it separate from the Blazor app until there is a concrete MCP client/use case.

## How to compare fairly

Run the same cases in `src/HuePilot.Eval/eval-cases.json` through both strategies, using the same resource catalog and fake Hue service. Record:

| Measure | What to compare |
| --- | --- |
| Task success | Correct action, target, and values for clear requests |
| Safe abstention | No mutation for ambiguous, unknown, unsupported, or out-of-range requests |
| Clarification quality | Whether the follow-up asks for the missing information |
| Tool efficiency | Number of model turns/tool calls; avoid unnecessary reads or repeated calls |
| Latency and cost | End-to-end time and model usage for each request |
| Testability | How easily each behavior can be tested without OpenAI or a physical Bridge |

The regular unit tests remain deterministic and offline. Live evaluation uses OpenAI and may incur charges, but should never execute Hue mutations.

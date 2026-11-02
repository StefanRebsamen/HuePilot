# Comparing AI integration approaches

Both approaches are implemented in the Blazor application and can be selected at runtime in the chat UI. The selected approach is persisted in protected browser storage, restored when the page reloads, and kept in scoped browser-circuit state for subsequent requests. If no choice has been saved yet, `AI:Approach` supplies the initial value. They share the same application-layer flow: the model interprets the request, the application resolves Hue resources, deterministic validation rejects unsafe proposals, and only confirmed actions are executed. The implementations are kept separate under `Infrastructure/Structured` and `Infrastructure/ToolCalling`, while `DynamicIntentParser` selects the active implementation for the browser circuit.

## 1. Structured intent parsing

### Request flow

1. `HueConversationService` loads the resources currently exposed by `IHueService`.
2. `OpenAiStructuredIntentParser` sends the latest request, recent conversation history, and a compact resource catalog to OpenAI.
3. The catalog contains resource names, types, and room/zone associations. No Hue identifiers, credentials, or live controls are sent to the browser.
4. JSON mode is requested and the model returns an `intents` array. Each intent contains an operation, target name and type, optional room/zone, brightness, and color. A request may therefore contain multiple intents; legacy single-intent responses are also accepted.
5. `HueConversationService` processes every intent independently. Resource names and associations are matched against Bridge data, ambiguity is rejected, and `HueActionValidator` checks operations, resource types, values, and the closed color vocabulary.
6. Read-only `GetStatus` intents retrieve current status. Mutation intents become a `ConversationPlan`, are displayed to the user, and then executed through `IHueService`.

For example, “Turn on the desk lamp and activate Relax in the office” should produce two intents. Both must pass resource resolution and validation before either action is executed.

### Properties and tradeoffs

This approach uses exactly one provider call per request. It is simple to log, test, evaluate, and explain because the complete plan is returned at once. It does not give the model an opportunity to query data it did not receive in the initial catalog; therefore discovery occurs in the application before the provider call rather than by the model during planning.

It is well suited for a prompt that is mostly understood as “interpret this request as one bounded plan.” Its main risk is an incomplete or hallucinated first snapshot. Final safety still depends on application-side resolution and validation, not on JSON syntax alone.

## 2. Model tool/function calling

### Request flow

1. `OpenAiToolCallingIntentParser` sends the latest request and recent conversation to OpenAI, together with a bounded set of typed tools.
2. The model may call `lookup_resources` to search the supplied application catalog by name, resource type, and room/zone association. The lookup returns resource IDs from discovered Bridge data only.
3. To propose an action, the model calls `propose_hue_action`. Its arguments identify an operation and opaque resource ID, plus optional brightness or color values.
4. The tool result or proposal is fed back to the provider in an application-controlled loop. HuePilot limits the loop to eight model turns and twenty-five lookup results per search. Parallel tool calls are enabled so compound requests can be planned in one response.
5. Every proposal is converted into a Hue intent and validated before it is retained. The parser collects multiple valid proposals when the user requests multiple actions and requires resource lookups for each named target.
6. When the model finishes, returns a clarification, or produces no more tool calls, the collected intents are passed to `HueConversationService` for the same resource resolution and validation used by structured output.
7. Status requests are resolved without mutation. Mutation actions are displayed and executed sequentially through `IHueService`.

This is a small agent loop, not direct execution by the AI: `propose_hue_action` creates only an in-memory proposal. Looking up resources also returns read-only data; the provider never receives Bridge credentials or direct network access.

### Properties and tradeoffs

Tool calling lets the model decide whether it needs an additional resource lookup and supports incremental planning. It can be a useful demonstration of agent behavior because tool calls and results can be shown in the chat when conversation logging is enabled.

It adds loop handling, provider round trips, latency, token usage, and the possibility of unnecessary tool calls. The application therefore imposes a turn limit, an explicit tool allowlist, bounded lookup results, one proposal tool, and final application-side validation. These controls keep the approach educational and safe without relying on the model alone.

## Shared safety boundary

Both approaches use the same deterministic guarantees:

- Only known Hue operations are accepted.
- Resource names resolve to resources discovered by `IHueService`; opaque IDs and text from the model are not trusted.
- Room, zone, light, and scene relationships are checked against Bridge data.
- Ambiguous or unknown targets ask for clarification instead of guessing.
- Brightness and supported color values are range- and vocabulary-checked.
- Status reads are limited to rooms and lights.
- Mutations are shown before execution and execute through the single `IHueService` boundary.
- Credentials and raw Hue networking remain on the server, outside the browser and AI prompt context.

## 3. MCP — a complementary protocol

MCP standardizes how a client discovers and invokes tools/resources exposed by a server. It is not itself a planning strategy and does not replace either structured intent or tool calling. HuePilot provides a local MCP server that exposes the same bounded Hue application service to compatible external agents such as OpenCode.

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

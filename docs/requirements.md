# HuePilot requirements (initial)

## Goal

Learn agentic development and application AI integration by building a Blazor application that controls Philips Hue lights through natural-language requests.

## Product scope

- The primary interface is a chat-like conversation, not a manual light dashboard.
- Provide a separate read-only resources page that lists discovered rooms, lights, and scenes, with a refresh action and a clear demo-mode indicator.
- Preserve and display associations between lights/scenes and their rooms or zones. Use those associations to disambiguate repeated scene names and ensure scene activation targets the intended room/group.
- Resolve user references against Hue-discovered room, light, and scene names.
- Support on/off, brightness, color, and activation of existing scenes.
- Show the interpreted target and action before executing it, then show the result.
- Clear requests execute without an extra confirmation. If target or intent is ambiguous, ask a follow-up and do not change Hue.
- Support follow-up context within the current session (for example, “make it warmer”); do not persist conversation history.
- On failures, show a plain-language explanation and make technical details expandable.
- Use OpenAI as the first concrete AI provider, behind a replaceable provider interface. Store its key in server-side development secrets, never source control.
- Tests must work without a physical Hue Bridge or live AI service.

## Initial acceptance criteria

1. A user can submit a natural-language request and see it in the conversation.
2. A clear request is translated into a bounded, structured action referencing a discovered Hue resource, and the UI displays the interpretation before execution.
3. Only validated on/off, brightness, color, and existing-scene operations can reach the Hue integration.
4. An unknown or ambiguous target produces a clarification question and causes no Hue mutation.
5. Follow-up requests can use current-session conversation context, but a new session starts without prior context.
6. Hue/AI failures are communicated in plain language with technical details available on demand.
7. Automated tests cover interpretation, validation, ambiguity, execution, and failure paths using fakes.
8. The resources page displays light membership and each scene's associated room/zone; an ambiguous repeated scene name asks which associated room/group is intended.

## Open questions

- Hue API generation and bridge discovery/credential onboarding.
- Exact OpenAI model/configuration. The initial implementation uses structured intent parsing; constrained tool calling is the comparison iteration.
- Exact supported color representations and handling of unsupported bulb capabilities.
- Authentication and deployment beyond local use (currently out of scope).

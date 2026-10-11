# HuePilot project guidance

## Project
- HuePilot is a learning application for AI-assisted development and AI features in an application.
- It is a .NET 10 Blazor Web App using server interactivity.
- Keep the AI provider replaceable and Hue access behind an application-owned abstraction.
- Prefer small, explainable increments over broad scaffolding.

## Safety and privacy
- Treat Hue Bridge credentials and AI provider keys as secrets. Never commit them or put them in browser-side code.
- AI output is untrusted input. Validate target IDs against resources discovered from Hue and validate every requested operation and value before execution.
- Initial allowed AI operations: on/off, brightness, color, and activation of existing scenes.
- Ask a clarifying question and do not execute when the target or requested operation is ambiguous.
- Do not persist conversation history or activity unless requirements change.
- Never allow an AI component to construct arbitrary URLs, access the filesystem, or call unrestricted APIs.

## Engineering
- Follow the existing .NET conventions and keep UI, application logic, Hue integration, and AI-provider concerns separated.
- Use dependency injection and interfaces at external boundaries so Hue and AI behavior can be faked in tests.
- Add or update tests for behavior and validation; tests must not require a physical Hue Bridge or live AI service.
- Run the relevant tests and build after changes, and report commands/results accurately.
- Explain important tradeoffs and ask before adding persistence, authentication, or remote access.

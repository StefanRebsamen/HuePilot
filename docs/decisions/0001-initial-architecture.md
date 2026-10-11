# ADR 0001: Initial application architecture

- **Status:** Accepted for the learning prototype
- **Date:** 2026-10-10

## Context

HuePilot is a local-use Blazor chat application. It needs to control Hue resources safely, keep AI providers replaceable, and support deterministic tests without live services. The learning goal includes comparing two AI integration patterns.

## Decision

- Use the .NET 10 Blazor Web App with server interactivity. Keep Hue and AI credentials and network calls on the server.
- Use Philips Hue Bridge API v2 behind an application-owned Hue service boundary.
- Use the HueApi .NET library for the local v2 protocol, Bridge discovery, and pairing; keep its models and calls inside the infrastructure adapter.
- Start with provider-neutral structured intent parsing. Validate the returned intent and resource IDs in application code before invoking a narrowly scoped Hue operation.
- Use OpenAI as the first concrete provider, configured on the server; keep provider selection behind the intent-parser interface.
- Add a separate constrained tool-calling implementation afterward, then compare behavior and tradeoffs.
- Keep orchestration and Hue/AI boundaries injectable so tests can use fakes and do not require a Bridge or live AI provider.
- Keep conversational context in current-session memory only. Do not persist chat or action history.
- For clear requests, show the interpreted action and execute it; for ambiguous requests, ask a clarification and do not mutate Hue state.

## Consequences

- Server-side interactive hosting simplifies local-network Bridge access and protects credentials from browser code.
- Provider neutrality adds a small abstraction now; a concrete model/provider is still needed before live AI requests can be exercised.
- The application, not the model, owns resource resolution, capability/range checks, and the final allowlist of Hue operations.
- Session-only context avoids database setup but will not survive application restarts and requires care to avoid cross-user conversation leakage if hosting scope changes.
- Hue Bridge onboarding, discovery, credential storage, TLS behavior, and unsupported bulb capabilities remain detailed design items before live integration.

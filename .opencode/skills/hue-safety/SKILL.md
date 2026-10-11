---
name: hue-safety
description: Review Hue and AI action flows for bounded, validated control.
---

# Hue and AI safety

The model is not authoritative. Resolve actions to discovered Hue IDs in application code; reject unknown IDs, unsupported capabilities, malformed values, and out-of-range brightness/color data. Restrict tools to listing relevant resources and the explicitly allowed operations: on/off, brightness, color, and activating existing scenes. Never expose raw Hue API access or credentials to the model/browser. If target resolution or intent is ambiguous, ask the user and make no change. Keep calls bounded, handle bridge/provider failures, and test validation with fakes.

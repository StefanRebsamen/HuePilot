using HuePilot.Application.Hue.Models;

namespace HuePilot.Application.Chat;

/// <summary>The assistant's proposed action or clarification, before any Hue mutation is executed.</summary>
/// <param name="AssistantMessage">Text shown in the chat.</param>
/// <param name="NeedsClarification">Whether the user must clarify before proceeding.</param>
/// <param name="ProposedActions">All validated actions proposed for one request.</param>
public sealed record ConversationPlan(
    string AssistantMessage,
    bool NeedsClarification = false,
    IReadOnlyList<HueAction>? ProposedActions = null);
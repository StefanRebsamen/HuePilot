namespace HuePilot.Application;

/// <summary>The assistant's proposed action or clarification, before any Hue mutation is executed.</summary>
/// <param name="AssistantMessage">Text shown in the chat.</param>
/// <param name="ProposedAction">A validated action awaiting the UI's execution step, if any.</param>
/// <param name="NeedsClarification">Whether the user must clarify before proceeding.</param>
public sealed record ConversationPlan(string AssistantMessage, HueAction? ProposedAction = null, bool NeedsClarification = false);

namespace HuePilot.Application;

/// <summary>One user or assistant utterance retained for the current chat session.</summary>
/// <param name="Role">The speaker, normally <c>user</c> or <c>assistant</c>.</param>
/// <param name="Content">The utterance text.</param>
public sealed record ChatTurn(string Role, string Content);

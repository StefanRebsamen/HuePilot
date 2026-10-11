using HuePilot.Application;

/// <summary>One natural-language case and its expected safe planning outcome.</summary>
/// <param name="Name">Short label printed in evaluation results.</param>
/// <param name="Request">The latest user message sent to the planner.</param>
/// <param name="Expected">The action or clarification behavior expected from the planner.</param>
/// <param name="History">Optional prior chat turns used to test contextual follow-ups.</param>
internal sealed record EvalCase(string Name, string Request, EvalExpectation Expected, ChatTurn[]? History = null);

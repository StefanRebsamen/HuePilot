namespace HuePilot.Application;

/// <summary>Converts natural-language requests into structured Hue intents.</summary>
public interface IIntentParser
{
    /// <summary>
    /// Interprets a user request using the available resource catalog and recent conversation.
    /// Implementations must treat model output as untrusted; application code performs final validation.
    /// </summary>
    /// <param name="request">The latest user message.</param>
    /// <param name="history">Prior chat turns from the current session.</param>
    /// <param name="availableResources">Resources discovered by the Hue service.</param>
    /// <param name="cancellationToken">Cancels the provider request when the caller is canceled.</param>
    /// <returns>A proposed intent or a clarification question.</returns>
    Task<HueIntent> ParseAsync(
        string request,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources,
        CancellationToken cancellationToken = default);
}

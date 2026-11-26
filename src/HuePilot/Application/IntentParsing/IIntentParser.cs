using HuePilot.Application.Chat;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;

namespace HuePilot.Application.IntentParsing;

/// <summary>Converts natural-language requests into structured Hue intents.</summary>
public interface IIntentParser
{
    /// <summary>
    /// Interprets a user request using the available resource catalog and recent conversation. Implementations must
    /// resolve names against this catalog and return ID-based intents or a clarification for ambiguous matches.
    /// Application code performs final validation.
    /// </summary>
    /// <param name="request">The latest user message.</param>
    /// <param name="history">Prior chat turns from the current session.</param>
    /// <param name="availableResources">Resources discovered by the Hue service.</param>
    /// <param name="cancellationToken">Cancels the provider request when the caller is canceled.</param>
    /// <returns>Proposed intents, or a single clarification intent when the request is ambiguous.</returns>
    Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default);
}
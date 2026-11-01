using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.Chat;
using HuePilot.Application;

namespace HuePilot.Infrastructure.Structured;

/// <summary>Uses OpenAI structured JSON output to translate a request into one Hue intent.</summary>
/// <param name="configuration">Configuration containing the OpenAI key and optional model name.</param>
public sealed class OpenAiStructuredIntentParser(IConfiguration configuration) : IIntentParser, IAiCallDiagnostics
{
    private const string SystemPrompt = """
        Convert the latest user request into one safe Philips Hue intent. Use only resources listed in the supplied catalog.
        Resource groups describe where a light belongs or which room/zone a scene controls. Use the user's requested room/group to disambiguate duplicate resource names.
        If the target is still ambiguous, the action is unsupported, or a value is outside the supported range,
        return a concise clarificationQuestion and no operation. Do not guess, clamp, or silently substitute values.
        Report GetStatus for requests asking whether a light or room is on or off, or for its current brightness.
        Supported colors are red, orange, yellow, green, cyan, blue, purple, pink, white, warm white, and cool white.
        Return JSON with an "intents" array. For each action use fields: operation (TurnOn, TurnOff, SetBrightness, SetColor, ActivateScene, GetStatus, or null),
        targetName, targetType (room, light, or scene), targetGroupName, targetGroupType (room or zone, or null),
        brightnessPercent (integer 1-100 or null), color (plain color name or null), clarificationQuestion.
        Never invent resource names, IDs, or associations.
        """;

    /// <inheritdoc />
    public int LastModelCallCount { get; private set; }

    /// <inheritdoc />
    public int LastToolCallCount => 0;

    /// <inheritdoc />
    public string LastConversationLog { get; private set; } = "";

    /// <inheritdoc />
    public async Task<HueIntent> ParseAsync(
        string request,
        IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources,
        CancellationToken cancellationToken = default)
    {
        LastModelCallCount = 0;
        LastConversationLog = "";
        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI is not configured. Set the OpenAI:ApiKey user secret to enable AI requests.");

        var client = new ChatClient(configuration["OpenAI:Model"] ?? configuration["OPENAI_MODEL"] ?? "gpt-4.1-mini", apiKey);
        var messages = new List<ChatMessage> { new SystemChatMessage(SystemPrompt) };
        foreach (var turn in history.TakeLast(12))
        {
            if (turn.Role == "user") messages.Add(new UserChatMessage(turn.Content));
            else if (turn.Role == "assistant") messages.Add(new AssistantChatMessage(turn.Content));
        }

        var catalog = JsonSerializer.Serialize(availableResources.Select(resource => new
        {
            resource.Name,
            resource.Type,
            groups = (resource.Groups ?? []).Select(group => new { group.Name, group.Type })
        }));
        messages.Add(new UserChatMessage($"Available Hue resources: {catalog}\nLatest request: {request}"));
        var options = new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() };
        var completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        LastModelCallCount = 1;
        var json = completion.Value.Content.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("The AI provider returned no intent.");
        LastConversationLog = $"User request:\n{request}\n\nModel response:\n{json}";

        // JSON mode guarantees syntax, not safe semantics; HueConversationService resolves and validates the result.
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("intents", out var intentsElement))
            return JsonSerializer.Deserialize<HueIntent>(json, jsonOptions)
                ?? throw new InvalidOperationException("The AI provider returned an invalid intent.");

        var intents = JsonSerializer.Deserialize<List<HueIntent>>(intentsElement.GetRawText(), jsonOptions)
            ?? throw new InvalidOperationException("The AI provider returned an invalid intent list.");
        if (intents.Count == 0)
            throw new InvalidOperationException("The AI provider returned no intents.");
        var first = intents[0];
        return intents.Count == 1
            ? first
            : first with { AdditionalIntents = intents.Skip(1).ToArray() };
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using HuePilot.Application;
using HuePilot.Application.Hue;
using OpenAI.Chat;

namespace HuePilot.Infrastructure.IntentParsing;

/// <summary>
/// Uses OpenAI JSON mode to translate a request into one or more proposed Hue intents.
/// The model receives only the discovered resource catalog; returned intents remain untrusted until the
/// application layer resolves their targets and validates each operation.
/// </summary>
/// <param name="configuration">Configuration containing the OpenAI key and optional model name.</param>
public sealed class OpenAiStructuredIntentParser(IConfiguration configuration) : IIntentParser, IAiCallDiagnostics
{
    private const string SystemPrompt = """
                                        Convert the latest user request into one safe Philips Hue intent. Use only resources listed in the supplied catalog.
                                        Resource groups describe where a light belongs or which room/zone a scene controls. Use the user's requested room/group to disambiguate duplicate resource names.
                                        If the target is still ambiguous, the action is unsupported, or a value is outside the supported range,
                                        return a concise clarificationQuestion and no operation. Do not guess, clamp, or silently substitute values.
                                        Report GetStatus for requests asking whether a light, room, or zone is on or off, or for its current brightness.
                                        Report ListResources for catalog questions such as what lights, rooms, zones, or scenes are available. For ListResources, targetType is the requested category (light, room, scene, or zone), and targetName is an optional room or zone used as the scope; targetGroupName can contain the same scope.
                                        Supported colors are red, orange, yellow, green, cyan, blue, purple, pink, white, warm white, and cool white.
                                        Return JSON with an "intents" array. For each action use fields: operation (TurnOn, TurnOff, SetBrightness, SetColor, ActivateScene, GetStatus, ListResources, or null),
                                        targetName, targetType (room, zone, light, or scene), targetGroupName, targetGroupType (room or zone, or null),
                                        brightnessPercent (integer 1-100 or null), color (plain color name or null), clarificationQuestion.
                                        Never invent resource names, IDs, or associations.
                                        """;

    /// <inheritdoc />
    public int LastModelCallCount { get; private set; }

    /// <inheritdoc />
    public int LastToolCallCount => 0;

    /// <inheritdoc />
    public string LastConversationLog { get; private set; } = "";

    /// <summary>Converts a request into proposed Hue intents using structured model output.</summary>
    public async Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources, CancellationToken cancellationToken = default)
    {
        ResetDiagnostics();
        var client = CreateClient();
        var messages = CreateMessages(request, history, availableResources);
        var completion = await client.CompleteChatAsync(messages, CreateOptions(), cancellationToken);
        LastModelCallCount = 1;

        var json = completion.Value.Content.FirstOrDefault()?.Text ??
                   throw new InvalidOperationException("The AI provider returned no intent.");
        LastConversationLog = $"User request:\n{request}\n\nModel response:\n{json}";
        return ParseIntents(json);
    }

    /// <summary>Resets diagnostics for a new parsing request.</summary>
    private void ResetDiagnostics()
    {
        LastModelCallCount = 0;
        LastConversationLog = "";
    }

    /// <summary>Creates the configured OpenAI chat client.</summary>
    private ChatClient CreateClient()
    {
        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI is not configured. Set the OpenAI:ApiKey user secret to enable AI requests.");
        }

        return new ChatClient(configuration["OpenAI:Model"] ?? configuration["OPENAI_MODEL"] ?? "gpt-4.1-mini",
            apiKey);
    }

    /// <summary>Builds the model messages from the request, history, and Hue catalog.</summary>
    private static List<ChatMessage> CreateMessages(string request, IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources)
    {
        var messages = new List<ChatMessage> { new SystemChatMessage(SystemPrompt) };
        foreach (var turn in history.TakeLast(12))
        {
            switch (turn.Role)
            {
                case "user":
                    messages.Add(new UserChatMessage(turn.Content));
                    break;
                case "assistant":
                    messages.Add(new AssistantChatMessage(turn.Content));
                    break;
            }
        }

        // The catalog is supplied as data in the final user message rather than as a model-accessible service. This
        // lets ChatClient send the context needed for name resolution without granting the model Bridge access.
        var catalog = JsonSerializer.Serialize(availableResources.Select(resource => new
        {
            resource.Name,
            resource.Type,
            groups = (resource.Groups ?? []).Select(group => new { group.Name, group.Type })
        }));
        messages.Add(new UserChatMessage($"Available Hue resources: {catalog}\nLatest request: {request}"));
        return messages;
    }

    /// <summary>Creates options that require JSON object responses.</summary>
    private static ChatCompletionOptions CreateOptions()
    {
        return new ChatCompletionOptions { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() };
    }

    /// <summary>Deserializes the model response into one or more Hue intents.</summary>
    private static IReadOnlyList<HueIntent> ParseIntents(string json)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("intents", out var intentsElement))
        {
            var singleIntent = JsonSerializer.Deserialize<HueIntent>(json, jsonOptions) ??
                               throw new InvalidOperationException("The AI provider returned an invalid intent.");
            return [singleIntent];
        }

        var intents = JsonSerializer.Deserialize<List<HueIntent>>(intentsElement.GetRawText(), jsonOptions) ??
                      throw new InvalidOperationException("The AI provider returned an invalid intent list.");
        if (intents.Count == 0)
        {
            throw new InvalidOperationException("The AI provider returned no intents.");
        }

        return intents;
    }
}

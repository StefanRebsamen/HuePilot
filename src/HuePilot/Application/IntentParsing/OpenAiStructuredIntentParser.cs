using System.Text.Json;
using HuePilot.Application.Chat;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;
using OpenAI.Chat;

namespace HuePilot.Application.IntentParsing;

/// <summary>
/// Uses OpenAI JSON mode to translate a request into one or more proposed Hue intents.
/// The model receives only the discovered resource catalog; returned intents remain untrusted until the
/// parser resolves names against the catalog and the application validates IDs and operations.
/// </summary>
/// <param name="configuration">Configuration containing the OpenAI key and optional model name.</param>
public sealed class OpenAiStructuredIntentParser(IConfiguration configuration)
    : OpenAiIntentParserBase(configuration), IAiCallDiagnostics
{
    private const string SystemPrompt = """
                                        Convert the latest user request into one or more safe Philips Hue intents. Use only resources listed in the supplied catalog.
                                        Resource groups describe where a light belongs or which room/zone a scene controls. Use the user's requested room/group to disambiguate duplicate resource names.
                                        If the target is still ambiguous, the action is unsupported, or a value is outside the supported range,
                                        return a concise clarificationQuestion and no operation. Do not guess, clamp, or silently substitute values.
                                        Report GetStatus for requests asking whether a light, room, or zone is on or off, or for its current brightness.
                                        Report ListResources for catalog questions such as what lights, rooms, zones, or scenes are available. For ListResources, use targetType for the requested category (light, room, scene, or zone), targetGroupName for the requested scope (null for a global list), and null for targetName.
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
    public override async Task<IReadOnlyList<HueIntent>> ParseAsync(string request, IReadOnlyList<ChatTurn> history,
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
        return ParseIntents(json, request, availableResources);
    }

    /// <summary>Resets diagnostics for a new parsing request.</summary>
    private void ResetDiagnostics()
    {
        LastModelCallCount = 0;
        LastConversationLog = "";
    }

    /// <summary>Builds the model messages from the request, history, and Hue catalog.</summary>
    private static List<ChatMessage> CreateMessages(string request, IReadOnlyList<ChatTurn> history,
        IReadOnlyList<HueResource> availableResources)
    {
        var messages = CreateConversationMessages(SystemPrompt, history);
        // The catalog is supplied as data in the final user message rather than as a model-accessible service. This
        // lets ChatClient send the context needed for name resolution without granting the model Bridge access.
        var groups = availableResources.OfType<HueResourceGroup>().ToArray();
        var catalog = JsonSerializer.Serialize(availableResources.Select(resource => new
        {
            resource.Name,
            resource.Type,
            groups = FindContainingGroups(groups, resource.Id)
                .Select(group => new { group.Id, group.Name, group.Type })
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
    internal static IReadOnlyList<HueIntent> ParseIntents(string json, string request,
        IReadOnlyList<HueResource> resources)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var proposals = document.RootElement.TryGetProperty("intents", out var intents)
                    ? intents.Deserialize<HueIntentProposal?[]>(HueIntentProposal.JsonOptions)
                    : [document.RootElement.Deserialize<HueIntentProposal>(HueIntentProposal.JsonOptions)];
                if (proposals is { Length: > 0 } && proposals.All(proposal => proposal is not null))
                {
                    var resolver = new HueIntentResolver(resources, request);
                    var resolved = proposals.Select(proposal => resolver.Resolve(proposal!, false)).ToArray();
                    return resolver.CheckCompleteGroupSelection(resolved);
                }
            }
        }
        catch (JsonException)
        {
            // Incomplete or unsupported model output requires clarification.
        }

        return [new ClarificationIntent("I couldn't read a supported Hue request. Could you rephrase it?")];
    }
}
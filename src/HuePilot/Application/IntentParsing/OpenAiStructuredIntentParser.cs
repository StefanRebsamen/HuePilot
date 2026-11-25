using System.Text.Json;
using System.Text.Json.Serialization;
using HuePilot.Application.Chat;
using HuePilot.Application.Hue.Models;
using OpenAI.Chat;

namespace HuePilot.Application.IntentParsing;

/// <summary>
/// Uses OpenAI JSON mode to translate a request into one or more proposed Hue intents.
/// The model receives only the discovered resource catalog; returned intents remain untrusted until the
/// application layer resolves their targets and validates each operation.
/// </summary>
/// <param name="configuration">Configuration containing the OpenAI key and optional model name.</param>
public sealed class OpenAiStructuredIntentParser(IConfiguration configuration)
    : OpenAiIntentParserBase(configuration), IAiCallDiagnostics
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
        return ParseIntents(json);
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
            var singleIntent = JsonSerializer.Deserialize<IntentProposal>(json, jsonOptions) ??
                               throw new InvalidOperationException("The AI provider returned an invalid intent.");
            return [CreateIntent(singleIntent)];
        }

        var intents = JsonSerializer.Deserialize<List<IntentProposal>>(intentsElement.GetRawText(), jsonOptions) ??
                      throw new InvalidOperationException("The AI provider returned an invalid intent list.");
        if (intents.Count == 0)
        {
            throw new InvalidOperationException("The AI provider returned no intents.");
        }

        return intents.Select(CreateIntent).ToArray();
    }

    private static HueIntent CreateIntent(IntentProposal proposal)
    {
        if (!string.IsNullOrWhiteSpace(proposal.ClarificationQuestion) || proposal.Operation is null)
        {
            return new ClarificationIntent(proposal.ClarificationQuestion ??
                "I couldn't determine the requested action. Could you rephrase it?");
        }

        if (!HueResourceTypeExtensions.TryParseHueV2WireType(proposal.TargetType, out var targetType))
        {
            return new ClarificationIntent("I couldn't determine the requested resource type. Could you rephrase it?");
        }

        if (!TryParseOptionalResourceType(proposal.TargetGroupType, out var targetGroupType))
        {
            return new ClarificationIntent("I couldn't determine the requested room or zone type. Could you rephrase it?");
        }

        var targetName = proposal.TargetName ?? "";
        return proposal.Operation.Value switch
        {
            HueOperation.TurnOn => new TurnOnIntent(targetName, targetType, proposal.TargetGroupName,
                targetGroupType),
            HueOperation.TurnOff => new TurnOffIntent(targetName, targetType, proposal.TargetGroupName,
                targetGroupType),
            HueOperation.SetBrightness => new SetBrightnessIntent(targetName, targetType, proposal.BrightnessPercent,
                proposal.TargetGroupName, targetGroupType),
            HueOperation.SetColor => new SetColorIntent(targetName, targetType, proposal.Color,
                proposal.TargetGroupName, targetGroupType),
            HueOperation.ActivateScene => new ActivateSceneIntent(targetName, targetType, proposal.TargetGroupName,
                targetGroupType),
            HueOperation.GetStatus => new GetStatusIntent(targetName, targetType, proposal.TargetGroupName,
                targetGroupType),
            HueOperation.ListResources => new ListResourcesIntent(proposal.TargetName, targetType,
                proposal.TargetGroupName, targetGroupType),
            _ => new ClarificationIntent("I couldn't determine the requested action. Could you rephrase it?")
        };
    }

    private static bool TryParseOptionalResourceType(string? identifier, out HueResourceType? type)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            type = null;
            return true;
        }

        if (HueResourceTypeExtensions.TryParseHueV2WireType(identifier, out var parsedType))
        {
            type = parsedType;
            return true;
        }

        type = null;
        return false;
    }

    private sealed record IntentProposal(
        HueOperation? Operation,
        string? TargetName,
        string? TargetType,
        int? BrightnessPercent,
        string? Color,
        string? TargetGroupName,
        string? TargetGroupType,
        string? ClarificationQuestion);
}

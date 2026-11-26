using System.Text.Json;
using System.Text.Json.Serialization;
using HuePilot.Application.Hue.Models;

namespace HuePilot.Application.IntentParsing;

/// <summary>Untrusted provider fields. Names and provider type hints stop at this boundary.</summary>
internal sealed record HueIntentProposal(HueOperation? Operation, string? TargetName, string? TargetType,
    string? TargetGroupName, string? TargetGroupType, Guid? TargetId, Guid? TargetGroupId,
    int? BrightnessPercent, string? Color, string? ClarificationQuestion)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
}
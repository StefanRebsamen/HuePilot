using HuePilot.Application;

/// <summary>The expected action fields or clarification requirement for an evaluation case.</summary>
internal sealed record EvalExpectation(
    bool NeedsClarification,
    HueOperation? Operation = null,
    string? TargetName = null,
    string? TargetType = null,
    int? BrightnessPercent = null,
    string? Color = null,
    string? TargetGroupName = null);

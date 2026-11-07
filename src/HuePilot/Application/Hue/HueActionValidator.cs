namespace HuePilot.Application.Hue;

/// <summary>Deterministic checks that constrain a model-proposed intent to supported Hue actions.</summary>
public static class HueActionValidator
{
    private static readonly string[] AllowedColors =
    [
        "red", "orange", "yellow", "green", "cyan", "blue", "purple", "pink", "white", "warm white", "cool white"
    ];

    private static readonly ValidationStage[] ValidationPipeline =
    [
        ValidateOperation,
        ValidateBrightness,
        ValidateColor,
        AcceptListResources,
        ValidateResourceType,
        ValidateStatusResourceType,
        ValidateSceneOperation
    ];

    /// <summary>Creates an executable action only when the intent matches the resolved resource and valid ranges.</summary>
    /// <param name="intent">The untrusted structured intent returned by an AI parser.</param>
    /// <param name="target">The exact Hue resource resolved by application code.</param>
    /// <param name="action">The validated action, or <see langword="null" /> when rejected.</param>
    /// <param name="error">A user-readable rejection reason, or an empty string on success.</param>
    /// <returns><see langword="true" /> when a safe action was created.</returns>
    public static bool TryCreate(HueIntent intent, HueResource target, out HueAction? action, out string error)
    {
        var result = ValidationPipeline
            .Select(stage => stage(intent, target))
            .FirstOrDefault(stageResult => stageResult.Outcome is not ValidationOutcome.Continue);

        if (result.Outcome is ValidationOutcome.Reject)
        {
            action = null;
            error = result.Error!;
            return false;
        }

        action = new HueAction(intent.Operation!.Value, target, intent.BrightnessPercent, intent.Color);
        error = string.Empty;
        return true;
    }

    private static ValidationResult ValidateOperation(HueIntent intent, HueResource _) =>
        intent.Operation is null
            ? ValidationResult.Reject("That action isn't supported.")
            : ValidationResult.Continue;

    private static ValidationResult ValidateBrightness(HueIntent intent, HueResource _) =>
        intent.Operation == HueOperation.SetBrightness && intent.BrightnessPercent is not (>= 1 and <= 100)
            ? ValidationResult.Reject("Brightness must be a whole-number percentage from 1 to 100.")
            : ValidationResult.Continue;

    private static ValidationResult ValidateColor(HueIntent intent, HueResource _) =>
        intent.Operation switch
        {
            not HueOperation.SetColor => ValidationResult.Continue,
            _ when string.IsNullOrWhiteSpace(intent.Color) =>
                ValidationResult.Reject("I couldn't determine a color. Which color should I use?"),
            _ when AllowedColors.Contains(intent.Color!.Trim(), StringComparer.OrdinalIgnoreCase) =>
                ValidationResult.Continue,
            _ => ValidationResult.Reject(
                "Please choose a named color such as red, orange, yellow, green, blue, purple, pink, or white.")
        };

    private static ValidationResult AcceptListResources(HueIntent intent, HueResource _) =>
        intent.Operation == HueOperation.ListResources
            ? ValidationResult.Accept
            : ValidationResult.Continue;

    private static ValidationResult ValidateResourceType(HueIntent _, HueResource target) =>
        target.Type is HueResourceTypes.Room or HueResourceTypes.Zone or HueResourceTypes.Light or HueResourceTypes.Scene
            ? ValidationResult.Continue
            : ValidationResult.Reject("That Hue resource type isn't supported.");

    private static ValidationResult ValidateStatusResourceType(HueIntent intent, HueResource target) =>
        intent.Operation != HueOperation.GetStatus ||
        target.Type is HueResourceTypes.Room or HueResourceTypes.Zone or HueResourceTypes.Light
            ? ValidationResult.Continue
            : ValidationResult.Reject("Hue status can only be reported for lights, rooms, and zones.");

    private static ValidationResult ValidateSceneOperation(HueIntent intent, HueResource target) =>
        (intent.Operation, IsScene(target)) switch
        {
            (HueOperation.ActivateScene, false) =>
                ValidationResult.Reject("Only an existing scene can be activated as a scene."),
            (not HueOperation.ActivateScene, true) =>
                ValidationResult.Reject("That operation can't be applied to a scene."),
            _ => ValidationResult.Continue
        };

    private static bool IsScene(HueResource target) =>
        target.Type.Equals(HueResourceTypes.Scene, StringComparison.OrdinalIgnoreCase);

    private delegate ValidationResult ValidationStage(HueIntent intent, HueResource target);

    private enum ValidationOutcome
    {
        Continue,
        Accept,
        Reject
    }

    private readonly record struct ValidationResult(ValidationOutcome Outcome, string? Error = null)
    {
        public static ValidationResult Continue => new(ValidationOutcome.Continue);
        public static ValidationResult Accept => new(ValidationOutcome.Accept);
        public static ValidationResult Reject(string error) => new(ValidationOutcome.Reject, error);
    }
}

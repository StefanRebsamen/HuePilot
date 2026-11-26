using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;

namespace HuePilot.Tests;

public sealed class HueActionValidatorTests
{
    private static readonly HueResource Office = new(Guid.NewGuid(), "Office", HueResourceType.Room);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void TryCreate_BrightnessIsOutsideAllowedRange_ReturnsValidationError(int brightness)
    {
        var intent = new SetBrightnessIntent(Office.Id, brightness);

        Assert.False(HueActionValidator.TryCreate(intent, Office, out _, out var error));
        Assert.Contains("1 to 100", error);
    }

    [Fact]
    public void TryCreate_ColorIsUnsupported_ReturnsValidationError()
    {
        var intent = new SetColorIntent(Office.Id, "ultraviolet");

        Assert.False(HueActionValidator.TryCreate(intent, Office, out _, out var error));
        Assert.Contains("named color", error);
    }

    [Fact]
    public void TryCreate_IntentTargetsAnotherResource_RejectsAction()
    {
        var intent = new TurnOnIntent(Guid.NewGuid());

        Assert.False(HueActionValidator.TryCreate(intent, Office, out var action, out _));
        Assert.Null(action);
    }
}
using HuePilot.Application.Hue;

namespace HuePilot.Tests;

public sealed class HueActionValidatorTests
{
    private static readonly HueResource Office = new("room-1", "Office", "room");

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void TryCreate_BrightnessIsOutsideAllowedRange_ReturnsValidationError(int brightness)
    {
        var intent = new HueIntent(HueOperation.SetBrightness, "Office", "room", brightness);

        Assert.False(HueActionValidator.TryCreate(intent, Office, out _, out var error));
        Assert.Contains("1 to 100", error);
    }

    [Fact]
    public void TryCreate_ColorIsUnsupported_ReturnsValidationError()
    {
        var intent = new HueIntent(HueOperation.SetColor, "Office", "room", Color: "ultraviolet");

        Assert.False(HueActionValidator.TryCreate(intent, Office, out _, out var error));
        Assert.Contains("named color", error);
    }

    [Fact]
    public void TryCreate_ListResources_SkipsResourceTypeValidation()
    {
        var unsupportedResource = new HueResource("device-1", "Bridge", "device");
        var intent = new HueIntent(HueOperation.ListResources, "Bridge", "device");

        Assert.True(HueActionValidator.TryCreate(intent, unsupportedResource, out var action, out var error));
        Assert.Equal(HueOperation.ListResources, action?.Operation);
        Assert.Empty(error);
    }
}

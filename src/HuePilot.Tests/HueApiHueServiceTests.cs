using System.Reflection;
using System.Text.Json;
using HueApi.Models.Requests;
using HuePilot.Application.Hue;
using HuePilot.Infrastructure.Hue;
using Microsoft.Extensions.Configuration;

namespace HuePilot.Tests;

public sealed class HueApiHueServiceTests
{
    private static readonly HueResource Office = new("room-1", "Office", "room");

    [Fact]
    public async Task GetResourcesAsync_BridgeSecretsAreMissing_ThrowsConfigurationException()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hue = new HueApiHueService(configuration);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => hue.GetResourcesAsync());

        Assert.Contains("/setup", error.Message);
    }

    [Fact]
    public void BuildLightUpdate_SetColor_ProducesHueXyColorPayload()
    {
        var method = typeof(HueApiHueService).GetMethod("BuildLightUpdate",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var action = new HueAction(HueOperation.SetColor, Office, Color: "red");

        var update = Assert.IsType<UpdateLight>(method.Invoke(null, [action]));
        var payload = JsonSerializer.SerializeToElement(update);

        var xy = payload.GetProperty("color").GetProperty("xy");
        Assert.Equal(.675, xy.GetProperty("x").GetDouble(), 3);
        Assert.Equal(.322, xy.GetProperty("y").GetDouble(), 3);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("color_temperature").ValueKind);
    }

    [Fact]
    public void BuildGroupedUpdate_SetColor_ProducesHueXyColorPayload()
    {
        var method = typeof(HueApiHueService).GetMethod("BuildGroupedUpdate",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var action = new HueAction(HueOperation.SetColor, Office, Color: "red");

        var update = Assert.IsType<UpdateGroupedLight>(method.Invoke(null, [action]));
        var payload = JsonSerializer.SerializeToElement(update);

        var xy = payload.GetProperty("color").GetProperty("xy");
        Assert.Equal(.675, xy.GetProperty("x").GetDouble(), 3);
        Assert.Equal(.322, xy.GetProperty("y").GetDouble(), 3);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("color_temperature").ValueKind);
    }
}

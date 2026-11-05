using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HuePilot.Simulator.Tests;

public sealed class HueV2EndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetRooms_ReturnsHueDataEnvelopeWithGroupedLightService()
    {
        var response = await _client.GetFromJsonAsync<HueEnvelope>('/' + "clip/v2/resource/room");

        Assert.NotNull(response);
        Assert.All(response.Data, room =>
        {
            Assert.Equal("room", room.GetProperty("type").GetString());
            Assert.Contains(room.GetProperty("services").EnumerateArray(), service =>
                service.GetProperty("rtype").GetString() == "grouped_light");
        });
    }

    [Theory]
    [InlineData("light")]
    [InlineData("zone")]
    [InlineData("scene")]
    [InlineData("device")]
    [InlineData("grouped_light")]
    public async Task GetResourceType_ReturnsHueDataEnvelope(string resourceType)
    {
        var response = await _client.GetFromJsonAsync<HueEnvelope>($"/clip/v2/resource/{resourceType}");

        Assert.NotNull(response);
        Assert.NotEmpty(response.Data);
    }

    [Fact]
    public async Task GetUnknownResourceType_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/clip/v2/resource/temperature");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_ReturnsSuccessfulHtmlResponse()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("HuePilot Bridge Simulator", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PutLight_ValidBrightness_UpdatesTheDiscoveredResource()
    {
        var light = (await _client.GetFromJsonAsync<HueEnvelope>("/clip/v2/resource/light"))!.Data.First();
        var id = light.GetProperty("id").GetString();

        var update = await _client.PutAsJsonAsync($"/clip/v2/resource/light/{id}",
            new { dimming = new { brightness = 42 } });
        var updated = (await _client.GetFromJsonAsync<HueEnvelope>("/clip/v2/resource/light"))!.Data
            .Single(resource => resource.GetProperty("id").GetString() == id);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(42, updated.GetProperty("dimming").GetProperty("brightness").GetDouble());
    }

    [Fact]
    public async Task PutLight_InvalidBrightness_RejectsWithoutMutation()
    {
        var light = (await _client.GetFromJsonAsync<HueEnvelope>("/clip/v2/resource/light"))!.Data.First();
        var id = light.GetProperty("id").GetString();
        var original = light.GetProperty("dimming").GetProperty("brightness").GetDouble();

        var update = await _client.PutAsJsonAsync($"/clip/v2/resource/light/{id}",
            new { dimming = new { brightness = 101 } });
        var current = (await _client.GetFromJsonAsync<HueEnvelope>("/clip/v2/resource/light"))!.Data
            .Single(resource => resource.GetProperty("id").GetString() == id);

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal(original, current.GetProperty("dimming").GetProperty("brightness").GetDouble());
    }

    public sealed record HueEnvelope(System.Text.Json.JsonElement[] Data);
}

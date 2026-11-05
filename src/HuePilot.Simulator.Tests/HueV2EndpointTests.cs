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

    public sealed record HueEnvelope(System.Text.Json.JsonElement[] Data);
}

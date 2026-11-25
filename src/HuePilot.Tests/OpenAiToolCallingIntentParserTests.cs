using System.Text.Json;
using HuePilot.Application.Hue;
using HuePilot.Application.Hue.Models;
using HuePilot.Application.IntentParsing;

namespace HuePilot.Tests;

public sealed class OpenAiToolCallingIntentParserTests
{
    [Fact]
    public void SearchResources_QueryTargetsRoomLights_ReturnsMemberLights()
    {
        var office = new HueResourceGroup("room-office", "Office", HueResourceType.Room);
        var resources = new HueResource[]
        {
            new("room-office", "Office", HueResourceType.Room), new("light-desk", "Desk lamp", HueResourceType.Light, [office]),
            new("light-ceiling", "Ceiling light", HueResourceType.Light, [office])
        };

        var json = OpenAiToolCallingIntentParser.LookupResources(
            """{"query":"Office","resourceType":"light","groupName":"","groupId":""}""", resources);
        using var document = JsonDocument.Parse(json);
        var matches = document.RootElement.GetProperty("resources").EnumerateArray().ToArray();

        Assert.Equal(2, matches.Length);
        Assert.Contains(matches, match => match.GetProperty("name").GetString() == "Desk lamp");
        Assert.Contains(matches, match => match.GetProperty("name").GetString() == "Ceiling light");
        Assert.Equal("room-office",
            document.RootElement.GetProperty("matchedGroups").EnumerateArray().Single().GetProperty("id").GetString());
    }
}

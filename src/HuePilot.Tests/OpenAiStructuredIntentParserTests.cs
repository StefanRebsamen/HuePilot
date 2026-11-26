using HuePilot.Application.Hue.Models;
using HuePilot.Application.Hue.Models.Intents;
using HuePilot.Application.IntentParsing;

namespace HuePilot.Tests;

public sealed class OpenAiStructuredIntentParserTests
{
    [Fact]
    public void ScopedBrightnessProposal_ResolvesTheNamedLightAndRoomToTheirIds()
    {
        var officeDesk = new HueResource(Guid.NewGuid(), "Desk", HueResourceType.Light);
        var kitchenDesk = new HueResource(Guid.NewGuid(), "Desk", HueResourceType.Light);
        HueResource[] catalog =
        [
            officeDesk, kitchenDesk,
            new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, [officeDesk])
        ];
        const string json =
            """{"intents":[{"operation":"SetBrightness","targetName":"Desk","targetType":"light","targetGroupName":"Office","brightnessPercent":60}]}""";

        var intent =
            Assert.Single(OpenAiStructuredIntentParser.ParseIntents(json, "Set the Desk in Office to 60%", catalog));

        Assert.Equal(new SetBrightnessIntent(officeDesk.Id, 60, catalog[2].Id), intent);
    }

    [Fact]
    public void CompoundRoomCommands_ResolveEachNamedTargetIndependently()
    {
        HueResource[] catalog =
        [
            new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, []),
            new HueResourceGroup(Guid.NewGuid(), "Kitchen", HueResourceType.Room, [])
        ];
        const string json =
            """{"intents":[{"operation":"TurnOn","targetName":"Office","targetType":"room"},{"operation":"TurnOff","targetName":"Kitchen","targetType":"room"}]}""";

        var intents = OpenAiStructuredIntentParser.ParseIntents(json, "Turn on Office and turn off Kitchen", catalog);

        Assert.Equal<HueIntent>([new TurnOnIntent(catalog[0].Id), new TurnOffIntent(catalog[1].Id)], intents);
    }

    [Fact]
    public void ListWithoutScope_DoesNotBorrowTheGroupOfAnotherCommand()
    {
        HueResource[] catalog = [new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, [])];
        const string json =
            """{"intents":[{"operation":"TurnOn","targetName":"Office","targetType":"room"},{"operation":"ListResources","targetType":"light"}]}""";

        var intents = OpenAiStructuredIntentParser.ParseIntents(json, "Turn on Office and list all lights", catalog);

        Assert.Equal<HueIntent>([new TurnOnIntent(catalog[0].Id), new ListResourcesIntent(HueResourceType.Light)],
            intents);
    }

    [Theory]
    [InlineData("Turn on Office or Kitchen", false)]
    [InlineData("Turn on Office or Kitchen", true)]
    [InlineData("Turn on Office and Kitchen", false)]
    public void MultipleRoomNames_ChoiceOrPartialPlan_AsksUser(string request, bool proposeBoth)
    {
        HueResource[] catalog =
        [
            new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, []),
            new HueResourceGroup(Guid.NewGuid(), "Kitchen", HueResourceType.Room, [])
        ];
        var json = proposeBoth
            ? """{"intents":[{"operation":"TurnOn","targetName":"Office","targetType":"room"},{"operation":"TurnOn","targetName":"Kitchen","targetType":"room"}]}"""
            : """{"operation":"TurnOn","targetName":"Office","targetType":"room"}""";

        var intents = OpenAiStructuredIntentParser.ParseIntents(json, request, catalog);

        Assert.IsType<ClarificationIntent>(Assert.Single(intents));
    }

    [Theory]
    [InlineData("""{"operation":"TurnOn","targetName":"Missing","targetType":"light"}""")]
    [InlineData("""{"operation":"TurnOn","targetName":"Desk","targetType":"light"}""")]
    [InlineData("""{"operation":"TurnOn","targetName":"Desk","targetType":"light","targetGroupName":"Missing"}""")]
    public void UnresolvableNames_ReturnClarification(string json)
    {
        HueResource[] catalog =
        [
            new(Guid.NewGuid(), "Desk", HueResourceType.Light), new(Guid.NewGuid(), "Desk", HueResourceType.Light)
        ];

        Assert.IsType<ClarificationIntent>(
            Assert.Single(OpenAiStructuredIntentParser.ParseIntents(json, "Turn on Desk", catalog)));
    }

    [Fact]
    public void NamedListScope_ProducesOnlyTheCategoryAndGroupId()
    {
        HueResource[] catalog = [new HueResourceGroup(Guid.NewGuid(), "Office", HueResourceType.Room, [])];
        const string json = """{"operation":"ListResources","targetType":"scene","targetGroupName":"Office"}""";

        Assert.Equal(new ListResourcesIntent(HueResourceType.Scene, catalog[0].Id),
            Assert.Single(OpenAiStructuredIntentParser.ParseIntents(json, "List scenes in Office", catalog)));
    }
}
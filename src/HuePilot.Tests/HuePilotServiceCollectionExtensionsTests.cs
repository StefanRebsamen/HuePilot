using HuePilot.Application;
using HuePilot.Application.Hue;
using HuePilot.Infrastructure;
using HuePilot.Infrastructure.Hue;
using HuePilot.Infrastructure.IntentParsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HuePilot.Tests;

public sealed class HuePilotServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHuePilotServices_BridgeCredentialsAreMissing_RegistersDemoHueServiceAndParserAliases()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddHuePilotServices(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        Assert.IsType<DemoHueService>(scopedServices.GetRequiredService<IHueService>());
        Assert.Same(scopedServices.GetRequiredService<DynamicIntentParser>(),
            scopedServices.GetRequiredService<IIntentParser>());
        Assert.Same(scopedServices.GetRequiredService<DynamicIntentParser>(),
            scopedServices.GetRequiredService<IAiCallDiagnostics>());
        Assert.IsType<HueConversationService>(scopedServices.GetRequiredService<HueConversationService>());
        Assert.IsType<HueBridgeSetupService>(scopedServices.GetRequiredService<HueBridgeSetupService>());
    }

    [Fact]
    public void AddHuePilotServices_BridgeCredentialsArePresent_RegistersHueApiService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bridge:IpAddress"] = "192.168.1.2", ["Bridge:ApplicationKey"] = "application-key"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddHuePilotServices(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<HueApiHueService>(scope.ServiceProvider.GetRequiredService<IHueService>());
    }
}

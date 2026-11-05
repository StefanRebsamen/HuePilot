using HuePilot.Application;
using HuePilot.Application.Hue;
using HuePilot.Infrastructure.Hue;
using HuePilot.Infrastructure.IntentParsing;

namespace HuePilot.Infrastructure;

/// <summary>Registers HuePilot application services and their infrastructure implementations.</summary>
public static class HuePilotServiceCollectionExtensions
{
    /// <summary>Adds the application services required by the HuePilot UI.</summary>
    public static IServiceCollection AddHuePilotServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AiApproachState>();
        services.AddScoped<DynamicIntentParser>();
        services.AddScoped<IIntentParser>(provider => provider.GetRequiredService<DynamicIntentParser>());
        services.AddScoped<IAiCallDiagnostics>(provider => provider.GetRequiredService<DynamicIntentParser>());

        if (!string.IsNullOrWhiteSpace(configuration["Bridge:IpAddress"]) &&
            !string.IsNullOrWhiteSpace(configuration["Bridge:ApplicationKey"]))
        {
            services.AddScoped<IHueService, HueApiHueService>();
        }
        else
        {
            services.AddScoped<IHueService, DemoHueService>();
        }

        services.AddScoped<HueBridgeSetupService>();
        services.AddScoped<HueConversationService>();

        return services;
    }
}

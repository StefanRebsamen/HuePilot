using HuePilot.Application.Hue;
using HuePilot.Infrastructure.Hue;
using HuePilot.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
AddHuePilotServices(builder.Configuration, builder.Services);

builder.Services.AddMcpServer().WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithToolsFromAssembly();

var app = builder.Build();
app.UseMcpLogging(app.Environment);
app.MapMcp("/mcp");

await app.RunAsync();

static void AddHuePilotServices(IConfigurationManager configuration, IServiceCollection services)
{
    // Reuse the Blazor app's secret store; never accept Bridge credentials from MCP arguments or requests.
    configuration.AddUserSecrets(typeof(HueApiHueService).Assembly, true);
    if (string.IsNullOrWhiteSpace(configuration["Bridge:IpAddress"]) ||
        string.IsNullOrWhiteSpace(configuration["Bridge:ApplicationKey"]))
    {
        throw new InvalidOperationException(
            "HuePilot MCP requires Bridge:IpAddress and Bridge:ApplicationKey user secrets.");
    }

    services.AddSingleton<IHueService, HueApiHueService>();
}

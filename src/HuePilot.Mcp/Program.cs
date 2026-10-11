using HuePilot.Application;
using HuePilot.Infrastructure;
using HuePilot.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var httpMode = args.Contains("--http", StringComparer.OrdinalIgnoreCase);
var hostArgs = args.Where(argument => !argument.Equals("--http", StringComparison.OrdinalIgnoreCase)).ToArray();

if (httpMode)
{
    // Loopback HTTP lets macOS Terminal-launched .NET access the Bridge while OpenCode connects locally.
    var builder = WebApplication.CreateBuilder(hostArgs);
    AddHuePilotServices(builder.Configuration, builder.Services);
    builder.Services.AddMcpServer()
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.MapMcp("/mcp");
    app.Urls.Clear();
    app.Urls.Add("http://127.0.0.1:5179");
    await app.RunAsync();
    return 0;
}

var stdioBuilder = Host.CreateApplicationBuilder(hostArgs);
stdioBuilder.Logging.ClearProviders();
stdioBuilder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
AddHuePilotServices(stdioBuilder.Configuration, stdioBuilder.Services);
stdioBuilder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await stdioBuilder.Build().RunAsync();
return 0;

static void AddHuePilotServices(IConfigurationManager configuration, IServiceCollection services)
{
    // Read HuePilot's user secrets; never accept Bridge credentials from MCP arguments or requests.
    configuration.AddUserSecrets(typeof(HueApiHueService).Assembly, optional: true);
    if (string.IsNullOrWhiteSpace(configuration["Bridge:IpAddress"]) ||
        string.IsNullOrWhiteSpace(configuration["Bridge:ApplicationKey"]))
        throw new InvalidOperationException("HuePilot MCP requires Bridge:IpAddress and Bridge:ApplicationKey user secrets.");

    services.AddSingleton<IHueService, HueApiHueService>();
}

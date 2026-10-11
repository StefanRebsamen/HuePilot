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

const string HttpModeArgument = "--http";

var httpMode = args.Contains(HttpModeArgument, StringComparer.OrdinalIgnoreCase);
var hostArgs = args.Where(argument => !argument.Equals(HttpModeArgument, StringComparison.OrdinalIgnoreCase)).ToArray();

return httpMode
    ? await RunHttpModeAsync(hostArgs)
    : await RunStdioModeAsync(hostArgs);

static async Task<int> RunHttpModeAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    AddHuePilotServices(builder.Configuration, builder.Services);

    builder.Services.AddMcpServer()
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.MapMcp("/mcp");

    // Bind only to loopback: OpenCode connects locally, while the server reaches Hue from its host process.
    app.Urls.Clear();
    app.Urls.Add("http://127.0.0.1:5179");

    await app.RunAsync();
    return 0;
}

static async Task<int> RunStdioModeAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    // MCP stdio reserves stdout for JSON-RPC; send all host logs to stderr.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

    AddHuePilotServices(builder.Configuration, builder.Services);
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
    return 0;
}

static void AddHuePilotServices(IConfigurationManager configuration, IServiceCollection services)
{
    // Reuse the Blazor app's secret store; never accept Bridge credentials from MCP arguments or requests.
    configuration.AddUserSecrets(typeof(HueApiHueService).Assembly, optional: true);
    if (string.IsNullOrWhiteSpace(configuration["Bridge:IpAddress"]) ||
        string.IsNullOrWhiteSpace(configuration["Bridge:ApplicationKey"]))
    {
        throw new InvalidOperationException("HuePilot MCP requires Bridge:IpAddress and Bridge:ApplicationKey user secrets.");
    }

    services.AddSingleton<IHueService, HueApiHueService>();
}

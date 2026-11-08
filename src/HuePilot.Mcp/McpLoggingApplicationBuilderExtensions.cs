using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace HuePilot.Mcp;

/// <summary>Provides registration helpers for development-only MCP payload logging.</summary>
public static class McpLoggingApplicationBuilderExtensions
{
    /// <summary>Adds bounded, redacted MCP payload logging when the host runs in Development.</summary>
    public static IApplicationBuilder UseMcpLogging(this IApplicationBuilder app, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            app.UseMiddleware<McpLoggingMiddleware>(new McpLoggingOptions());
        }

        return app;
    }
}

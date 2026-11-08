using HuePilot.Simulator.Simulation;
using HuePilot.Simulator.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(SimulatorState.CreateDefault());
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/clip/v2"))
    {
        app.Logger.LogInformation("Hue simulator API call: {Method} {Path}", context.Request.Method,
            context.Request.Path);
    }

    await next(context);
});
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapHueV2Endpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public partial class Program;

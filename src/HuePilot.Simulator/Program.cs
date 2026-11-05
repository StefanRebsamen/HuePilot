using HuePilot.Simulator.Simulation;
using HuePilot.Simulator.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(SimulatorState.CreateDefault());
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.MapHueV2Endpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public partial class Program;

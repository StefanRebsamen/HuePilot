using HuePilot.Simulator.Simulation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(SimulatorState.CreateDefault());

var app = builder.Build();
app.MapGet("/", () => Results.Text("HuePilot simulator is running."));
app.Run();

public partial class Program;

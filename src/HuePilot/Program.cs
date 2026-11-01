using HuePilot.Components;
using HuePilot.Application;
using HuePilot.Infrastructure;
using HuePilot.Infrastructure.Structured;
using HuePilot.Infrastructure.ToolCalling;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AiApproachState>();
builder.Services.AddScoped<DynamicIntentParser>();
builder.Services.AddScoped<IIntentParser>(sp => sp.GetRequiredService<DynamicIntentParser>());
builder.Services.AddScoped<IAiCallDiagnostics>(sp => sp.GetRequiredService<DynamicIntentParser>());
if (!string.IsNullOrWhiteSpace(builder.Configuration["Bridge:IpAddress"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["Bridge:ApplicationKey"]))
    builder.Services.AddScoped<IHueService, HueApiHueService>();
else
    builder.Services.AddScoped<IHueService, DemoHueService>();
builder.Services.AddScoped<HueBridgeSetupService>();
builder.Services.AddScoped<HueConversationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

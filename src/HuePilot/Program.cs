using HuePilot.Components;
using HuePilot.Application;
using HuePilot.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IIntentParser>(sp =>
    string.Equals(builder.Configuration["AI:Approach"], "tools", StringComparison.OrdinalIgnoreCase)
        ? new OpenAiToolCallingIntentParser(sp.GetRequiredService<IConfiguration>())
        : new OpenAiIntentParser(sp.GetRequiredService<IConfiguration>()));
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

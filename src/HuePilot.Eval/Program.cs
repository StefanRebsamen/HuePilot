using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using HuePilot.Application;
using HuePilot.Infrastructure;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(OpenAiIntentParser).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

if (string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]) && string.IsNullOrWhiteSpace(configuration["OPENAI_API_KEY"]))
{
    Console.Error.WriteLine("OpenAI key is missing. Set the HuePilot user secret OpenAI:ApiKey or OPENAI_API_KEY.");
    return 2;
}

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
jsonOptions.Converters.Add(new JsonStringEnumConverter());
var casesPath = Path.Combine(AppContext.BaseDirectory, "eval-cases.json");
var cases = JsonSerializer.Deserialize<EvalCase[]>(await File.ReadAllTextAsync(casesPath), jsonOptions)
    ?? throw new InvalidOperationException("The evaluation cases file is empty or invalid.");
var approach = args.FirstOrDefault()?.ToLowerInvariant() ?? "structured";
if (approach is not ("structured" or "tools"))
{
    Console.Error.WriteLine("Usage: dotnet run --project tests/HuePilot.Eval -- [structured|tools]");
    return 2;
}

var resources = new List<HueResource>
{
    new("room-bedroom", "Bedroom", "room"),
    new("room-kitchen", "Kitchen", "room"),
    new("room-living", "Living room", "room"),
    new("room-office", "Office", "room"),
    new("light-desk", "Desk", "light"),
    new("light-buddha", "Buddha", "light"),
    new("scene-relax-1", "Relax", "scene", [new("room-bedroom", "Bedroom", "room")]),
    new("scene-relax-2", "Relax", "scene", [new("room-kitchen", "Kitchen", "room")]),
    new("scene-relax-3", "Relax", "scene", [new("room-living", "Living room", "room")])
};

var hue = new FakeHueService(resources);
IIntentParser parser = approach == "tools"
    ? new OpenAiToolCallingIntentParser(configuration)
    : new OpenAiIntentParser(configuration);
var diagnostics = (IAiCallDiagnostics)parser;
var conversation = new HueConversationService(parser, hue);
var passed = 0;
var modelCalls = 0;
var toolCalls = 0;
var elapsedTotal = TimeSpan.Zero;

Console.WriteLine($"Running {cases.Length} '{approach}' intent evaluations. No Hue commands will be executed.\n");
foreach (var testCase in cases)
{
    diagnostics = (IAiCallDiagnostics)parser;
    var timer = Stopwatch.StartNew();
    try
    {
        var plan = await conversation.PlanAsync(testCase.Request, testCase.History ?? []);
        var actualNeedsClarification = plan.NeedsClarification || plan.ProposedAction is null;
        var expected = testCase.Expected;
        var matches = actualNeedsClarification == expected.NeedsClarification;

        if (!expected.NeedsClarification && plan.ProposedAction is { } action)
        {
            matches &= action.Operation == expected.Operation;
            matches &= EqualsIgnoreCase(action.Target.Name, expected.TargetName);
            matches &= EqualsIgnoreCase(action.Target.Type, expected.TargetType);
            if (expected.BrightnessPercent is not null) matches &= action.BrightnessPercent == expected.BrightnessPercent;
            if (expected.Color is not null) matches &= EqualsIgnoreCase(action.Color, expected.Color);
            if (expected.TargetGroupName is not null)
                matches &= (action.Target.Groups ?? []).Any(group => EqualsIgnoreCase(group.Name, expected.TargetGroupName));
        }

        if (matches)
        {
            passed++;
            Console.WriteLine($"PASS  {testCase.Name}: {plan.AssistantMessage} [{diagnostics.LastModelCallCount} model call(s), {diagnostics.LastToolCallCount} tool call(s)]");
        }
        else
        {
            var actual = plan.ProposedAction is { } a
                ? $"{a.Operation} {a.Target.Type} '{a.Target.Name}' brightness={a.BrightnessPercent} color={a.Color}"
                : $"clarification: {plan.AssistantMessage}";
            Console.WriteLine($"FAIL  {testCase.Name}: expected {ExpectedDescription(expected)}; got {actual}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL  {testCase.Name}: {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        timer.Stop();
        elapsedTotal += timer.Elapsed;
        modelCalls += diagnostics.LastModelCallCount;
        toolCalls += diagnostics.LastToolCallCount;
    }
}

Console.WriteLine($"\nResult: {passed}/{cases.Length} passed. Model calls: {modelCalls}. Tool calls: {toolCalls}. Elapsed: {elapsedTotal.TotalSeconds:F1}s. Hue mutations: {hue.Executed.Count}.");
return passed == cases.Length && hue.Executed.Count == 0 ? 0 : 1;

static bool EqualsIgnoreCase(string? left, string? right) =>
    string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

static string ExpectedDescription(EvalExpectation expected) => expected.NeedsClarification
    ? "a clarification with no action"
    : $"{expected.Operation} {expected.TargetType} '{expected.TargetName}'";

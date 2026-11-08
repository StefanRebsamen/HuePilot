using System.Text;
using HuePilot.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HuePilot.Tests;

public sealed class McpLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_McpRequestAndResponse_LogsRawPayloadsAndPreservesBodies()
    {
        const string requestPayload = "{\"jsonrpc\":\"2.0\",\"token\":\"request-secret\"}";
        const string responsePayload = "{\"result\":{\"Authorization\":\"response-secret\",\"ok\":true}}";
        var downstreamRequest = string.Empty;

        var result = await InvokeAsync("/mcp", requestPayload, responsePayload,
            async context => downstreamRequest = await new StreamReader(context.Request.Body).ReadToEndAsync());

        Assert.Equal(requestPayload, downstreamRequest);
        Assert.Equal(responsePayload, result.ResponseBody);
        Assert.Contains(result.LogMessages, message => message.Contains("MCP request payload") &&
                                                   message.Contains("\"token\": \"request-secret\""));
        Assert.Contains(result.LogMessages, message => message.Contains("MCP response payload") &&
                                                   message.Contains("\"Authorization\": \"response-secret\""));
    }

    [Fact]
    public async Task InvokeAsync_NonMcpRequest_DoesNotLogOrBufferPayload()
    {
        var result = await InvokeAsync("/health", "{\"token\":\"secret\"}", "ok");

        Assert.Equal("ok", result.ResponseBody);
        Assert.Empty(result.LogMessages);
    }

    [Fact]
    public async Task InvokeAsync_RequestExceedsConfiguredLimit_LogsTruncationWithoutPayload()
    {
        const string oversizedPayload = "{\"value\":\"this payload must not be logged\"}";

        var result = await InvokeAsync("/mcp", oversizedPayload, "{}", maximumPayloadBytes: 8);

        Assert.Contains(result.LogMessages, message => message.Contains("truncated") &&
                                                   !message.Contains("this payload must not be logged"));
    }

    [Fact]
    public async Task InvokeAsync_PayloadResemblingTruncationMarker_LogsItsRawText()
    {
        const string payload = "[truncated: password=request-secret]";

        var result = await InvokeAsync("/mcp", payload, payload);

        Assert.Equal(2, result.LogMessages.Count(message => message.Contains(payload)));
    }

    [Fact]
    public async Task InvokeAsync_SseJsonResponse_FormatsEnvelopeAndEmbeddedJsonText()
    {
        const string responsePayload = "event: message\ndata: {\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"[{\\\"Id\\\":1}]\"}]}}";

        var result = await InvokeAsync("/mcp", "{}", responsePayload);

        Assert.Contains(result.LogMessages, message => message.Contains("event: message\ndata: {\n") &&
                                                   message.Contains("\"text\": [\n") &&
                                                   message.Contains("\"Id\": 1"));
    }

    [Fact]
    public async Task InvokeAsync_JsonRequest_FormatsPayload()
    {
        var result = await InvokeAsync("/mcp", "{\"method\":\"tools/call\",\"id\":23}", "{}");

        Assert.Contains(result.LogMessages, message => message.Contains("MCP request payload: {\n") &&
                                                   message.Contains("\"method\": \"tools/call\""));
    }

    [Fact]
    public async Task InvokeAsync_NonSeekableRequest_PreservesNonSeekableDeliveryToDownstream()
    {
        const string payload = "{\"jsonrpc\":\"2.0\",\"id\":1}";
        var context = new DefaultHttpContext();
        context.Request.Path = "/mcp";
        context.Request.Body = new NonSeekableReadStream(payload);
        context.Response.Body = new MemoryStream();
        var logProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logProvider));
        var downstreamPayload = string.Empty;
        var downstreamCanSeek = true;
        var middleware = new McpLoggingMiddleware(
            async httpContext =>
            {
                downstreamCanSeek = httpContext.Request.Body.CanSeek;
                downstreamPayload = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
            },
            loggerFactory.CreateLogger<McpLoggingMiddleware>(),
            new McpLoggingOptions());

        await middleware.InvokeAsync(context);

        Assert.False(downstreamCanSeek);
        Assert.Equal(payload, downstreamPayload);
    }

    [Fact]
    public async Task InvokeAsync_DuplicateJsonProperties_DoNotMaskDownstreamExceptionOrChangePayload()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/mcp";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"token\":\"first\",\"token\":\"second\"}"));
        context.Response.Body = new MemoryStream();
        var logProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logProvider));
        var expected = new InvalidOperationException("downstream failure");
        var middleware = new McpLoggingMiddleware(
            async httpContext =>
            {
                await httpContext.Response.WriteAsync("{\"token\":\"first\",\"token\":\"second\"}");
                throw expected;
            },
            loggerFactory.CreateLogger<McpLoggingMiddleware>(),
            new McpLoggingOptions());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Same(expected, actual);
        Assert.Contains(logProvider.Messages, message => message.Contains("first") && message.Contains("second"));
    }

    [Theory]
    [InlineData("Development", 2)]
    [InlineData("Production", 0)]
    public async Task UseMcpLogging_EnvironmentControlsPayloadLogging(string environmentName, int expectedLogEntries)
    {
        var logProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logProvider));
        var application = new ApplicationBuilder(services.BuildServiceProvider());
        application.UseMcpLogging(new TestHostEnvironment { EnvironmentName = environmentName });
        application.Run(context => context.Response.WriteAsync("{}"));
        var context = new DefaultHttpContext();
        context.Request.Path = "/mcp";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();

        await application.Build()(context);

        Assert.Equal(expectedLogEntries, logProvider.Messages.Count);
    }

    private static async Task<InvocationResult> InvokeAsync(
        PathString path,
        string requestPayload,
        string responsePayload,
        RequestDelegate? inspectDownstreamRequest = null,
        int maximumPayloadBytes = 16 * 1024)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestPayload));
        context.Response.Body = new MemoryStream();

        var logProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logProvider));
        var middleware = new McpLoggingMiddleware(
            async httpContext =>
            {
                if (inspectDownstreamRequest is not null)
                {
                    await inspectDownstreamRequest(httpContext);
                }
                else
                {
                    await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                }

                await httpContext.Response.WriteAsync(responsePayload);
            },
            loggerFactory.CreateLogger<McpLoggingMiddleware>(),
            new McpLoggingOptions { MaximumPayloadBytes = maximumPayloadBytes });

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        return new InvocationResult(await new StreamReader(context.Response.Body).ReadToEndAsync(), logProvider.Messages);
    }

    private sealed record InvocationResult(string ResponseBody, IReadOnlyList<string> LogMessages);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "HuePilot.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream inner;

        public NonSeekableReadStream(string payload) => inner = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

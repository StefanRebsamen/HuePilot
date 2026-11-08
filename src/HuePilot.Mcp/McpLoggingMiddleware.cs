using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HuePilot.Mcp;

/// <summary>Configures the bounded payload capture performed for HTTP MCP traffic.</summary>
public sealed class McpLoggingOptions
{
    /// <summary>Gets the maximum number of request or response bytes retained for a log entry.</summary>
    public int MaximumPayloadBytes { get; init; } = 16 * 1024;
}

/// <summary>Logs bounded payloads exchanged with the HTTP MCP endpoint.</summary>
public sealed class McpLoggingMiddleware(
    RequestDelegate next,
    ILogger<McpLoggingMiddleware> logger,
    McpLoggingOptions options)
{
    /// <summary>Processes an HTTP request and logs its MCP payloads when applicable.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path != "/mcp" || !logger.IsEnabled(LogLevel.Information))
        {
            await next(context);
            return;
        }

        var maximumPayloadBytes = ValidateMaximumPayloadBytes();
        var originalRequestBody = context.Request.Body;
        var originalResponseBody = context.Response.Body;
        var requestCapture = new PayloadCapture(maximumPayloadBytes);
        var responseCapture = new PayloadCapture(maximumPayloadBytes);
        await using var requestCaptureStream = new PayloadCaptureReadStream(originalRequestBody, requestCapture);
        await using var responseCaptureStream = new PayloadCaptureStream(originalResponseBody, responseCapture);
        context.Request.Body = requestCaptureStream;
        context.Response.Body = responseCaptureStream;

        try
        {
            await next(context);
        }
        finally
        {
            context.Request.Body = originalRequestBody;
            context.Response.Body = originalResponseBody;
            logger.LogInformation("MCP request payload: {Payload}", FormatPayload(requestCapture.GetPayload()));
            logger.LogInformation("MCP response payload: {Payload}", FormatPayload(responseCapture.GetPayload()));
        }
    }

    private static string FormatPayload(CapturedPayload payload)
    {
        if (payload.IsTruncated)
        {
            return "[truncated: payload exceeds configured limit]";
        }

        try
        {
            var lines = payload.Content.Split('\n');
            var containsSseData = false;
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].StartsWith("data: ", StringComparison.Ordinal))
                {
                    continue;
                }

                var json = JsonNode.Parse(lines[index][6..]);
                FormatEmbeddedJson(json);
                lines[index] = $"data: {json!.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}";
                containsSseData = true;
            }

            if (containsSseData)
            {
                return string.Join('\n', lines);
            }

            var requestJson = JsonNode.Parse(payload.Content);
            FormatEmbeddedJson(requestJson);
            return requestJson!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return payload.Content;
        }
    }

    private static void FormatEmbeddedJson(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Key == "text" && property.Value is JsonValue value &&
                    value.TryGetValue<string>(out var text))
                {
                    try
                    {
                        obj[property.Key] = JsonNode.Parse(text);
                    }
                    catch (JsonException)
                    {
                        // Text content is not JSON; keep it unchanged.
                    }
                }
                else
                {
                    FormatEmbeddedJson(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                FormatEmbeddedJson(item);
            }
        }
    }

    private int ValidateMaximumPayloadBytes() =>
        options.MaximumPayloadBytes > 0
            ? options.MaximumPayloadBytes
            : throw new InvalidOperationException("MCP payload logging requires a positive maximum payload size.");

    private sealed record CapturedPayload(string Content, bool IsTruncated);

    private sealed class PayloadCapture(int maximumPayloadBytes)
    {
        private readonly MemoryStream capture = new(Math.Min(maximumPayloadBytes + 1, 16 * 1024));

        public void Capture(ReadOnlySpan<byte> bytes)
        {
            var remaining = maximumPayloadBytes + 1 - (int)capture.Length;
            if (remaining > 0)
            {
                capture.Write(bytes[..Math.Min(bytes.Length, remaining)]);
            }
        }

        public CapturedPayload GetPayload() =>
            new(Encoding.UTF8.GetString(capture.GetBuffer(), 0, (int)Math.Min(capture.Length, maximumPayloadBytes)),
                capture.Length > maximumPayloadBytes);
    }

    private sealed class PayloadCaptureReadStream(Stream innerStream, PayloadCapture capture) : Stream
    {
        public override bool CanRead => innerStream.CanRead;
        public override bool CanSeek => innerStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => innerStream.Length;
        public override long Position { get => innerStream.Position; set => innerStream.Position = value; }
        public override void Flush() => innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = innerStream.Read(buffer, offset, count);
            capture.Capture(buffer.AsSpan(offset, read));
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await innerStream.ReadAsync(buffer, cancellationToken);
            capture.Capture(buffer.Span[..read]);
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class PayloadCaptureStream(Stream innerStream, PayloadCapture capture) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => innerStream.CanWrite;
        public override long Length => innerStream.Length;
        public override long Position { get => innerStream.Position; set => throw new NotSupportedException(); }
        public override void Flush() => innerStream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => innerStream.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            innerStream.Write(buffer, offset, count);
            capture.Capture(buffer.AsSpan(offset, count));
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await innerStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
            capture.Capture(buffer.AsSpan(offset, count));
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await innerStream.WriteAsync(buffer, cancellationToken);
            capture.Capture(buffer.Span);
        }
    }
}

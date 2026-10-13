using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpClientTests
{
  [Fact]
  public async Task CallUsesBearerOnlyMcpAndRetainsWrappedAndStructuredContent()
  {
    var handler = new StubHandler(HttpStatusCode.OK,
        """{"jsonrpc":"2.0","id":"2","result":{"content":[{"type":"text","text":"<untrusted>member text</untrusted>"}],"structuredContent":{"post_id":"post-1"},"isError":false}}""");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);
    var arguments = JsonDocument.Parse("""{"id":"post-1"}""").RootElement;

    var result = await client.CallToolAsync("read_post", arguments, "access-1", TestContext.Current.CancellationToken);

    Assert.Equal("https://example.test/api/v1/mcp", handler.Url);
    Assert.Equal("Bearer access-1", handler.Authorization);
    Assert.Null(handler.Cookie);
    Assert.Equal("application/json, text/event-stream", handler.Accept);
    Assert.Equal("2025-06-18", handler.ProtocolVersion);
    Assert.Equal("test-session", handler.SessionId);
    Assert.Equal(["initialize", "notifications/initialized", "tools/call"], handler.Methods);
    using var body = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
    Assert.Equal("tools/call", body.RootElement.GetProperty("method").GetString());
    Assert.Equal("read_post", body.RootElement.GetProperty("params").GetProperty("name").GetString());
    Assert.Equal("post-1", body.RootElement.GetProperty("params").GetProperty("arguments").GetProperty("id").GetString());
    Assert.Equal("<untrusted>member text</untrusted>", result.Content[0].GetProperty("text").GetString());
    Assert.Equal("post-1", result.StructuredContent?.GetProperty("post_id").GetString());
  }

  [Fact]
  public async Task HttpRateLimitRetainsRetryAfter()
  {
    var handler = new StubHandler(HttpStatusCode.TooManyRequests, "") { RetryAfterSeconds = 45 };
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);

    var error = await Assert.ThrowsAsync<McpRateLimitException>(
        () => client.ListToolsAsync("access-1", TestContext.Current.CancellationToken));

    Assert.Equal(TimeSpan.FromSeconds(45), error.RetryAfter);
  }

  [Fact]
  public async Task ExpiredSessionAfterRetryUsesTypedTransportError()
  {
    var handler = new StubHandler(HttpStatusCode.NotFound, "");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);

    var error = await Assert.ThrowsAsync<McpSessionExpiredException>(
        () => client.ListToolsAsync("access-1", TestContext.Current.CancellationToken));

    Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    Assert.Equal(2, handler.Methods.Count(method => method == "tools/list"));
  }

  [Fact]
  public async Task ToolListingForwardsOpaqueCursor()
  {
    var handler = new StubHandler(HttpStatusCode.OK,
        """{"jsonrpc":"2.0","id":"2","result":{"tools":[],"nextCursor":"next/+"}}""");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);

    var result = await client.ListToolsAsync("access-1", "opaque/+", TestContext.Current.CancellationToken);

    using var body = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
    Assert.Equal("opaque/+", body.RootElement.GetProperty("params").GetProperty("cursor").GetString());
    Assert.Equal("next/+", result.GetProperty("nextCursor").GetString());
  }

  [Fact]
  public async Task InBandRateLimitRetainsRetryAfter()
  {
    var inner = """{"error":{"status":429,"code":"RATE_LIMIT","retryAfterSeconds":17}}""";
    var body = JsonSerializer.Serialize(new
    {
      jsonrpc = "2.0",
      id = "2",
      result = new { isError = true, content = new[] { new { type = "text", text = inner } } }
    });
    var handler = new StubHandler(HttpStatusCode.OK, body);
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);
    var arguments = JsonDocument.Parse("{}").RootElement;

    var error = await Assert.ThrowsAsync<McpRateLimitException>(
        () => client.CallToolAsync("read_post", arguments, "access-1", TestContext.Current.CancellationToken));

    Assert.Equal(TimeSpan.FromSeconds(17), error.RetryAfter);
  }

  [Fact]
  public async Task MalformedInBandErrorRemainsWrappedAndHttpDateWaitIsPositive()
  {
    var malformed = """{"jsonrpc":"2.0","id":"2","result":{"isError":true,"content":[{"type":"text","text":"{\"error\":[]}"}]}}""";
    using (var client = new MemberMcpClient(new Uri("https://example.test"),
        new StubHandler(HttpStatusCode.OK, malformed)))
    {
      var argument = JsonDocument.Parse("{}").RootElement;
      var result = await client.CallToolAsync("read_post", argument, "access-1", TestContext.Current.CancellationToken);
      Assert.True(result.IsError);
      Assert.Equal("{\"error\":[]}", result.Content[0].GetProperty("text").GetString());
    }
    using var limited = new MemberMcpClient(new Uri("https://example.test"),
        new StubHandler(HttpStatusCode.TooManyRequests, "")
        {
          RetryAfterDate = DateTimeOffset.UtcNow.AddMinutes(2)
        });
    var error = await Assert.ThrowsAsync<McpRateLimitException>(
        () => limited.ListToolsAsync("access-1", TestContext.Current.CancellationToken));
    Assert.InRange(error.RetryAfter!.Value.TotalSeconds, 60, 120);
  }

  [Fact]
  public async Task SseSkipsNotificationsAndUnmatchedIdsUntilTheRequestedResult()
  {
    var events = ": heartbeat\n\n" +
        "data: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\n\n" +
        "data: {\"jsonrpc\":\"2.0\",\"id\":\"other\",\"result\":{}}\n\n" +
        "data: {\"jsonrpc\":\"2.0\",\"id\":\"2\",\"result\":{\"tools\":[{\"name\":\"read_post\"}]}}\n\n";
    using var client = new MemberMcpClient(new Uri("https://example.test"),
        new StubHandler(HttpStatusCode.OK, events) { ContentType = "text/event-stream" });

    var result = await client.ListToolsAsync("access-1", TestContext.Current.CancellationToken);

    Assert.Equal("read_post", result.GetProperty("tools")[0].GetProperty("name").GetString());
  }

  [Fact]
  public async Task SseAnswersServerPingBeforeToolResult()
  {
    var events = "data: {\"jsonrpc\":\"2.0\",\"id\":17,\"method\":\"ping\"}\n\n" +
        "data: {\"jsonrpc\":\"2.0\",\"id\":\"2\",\"result\":{\"tools\":[]}}\n\n";
    var handler = new StubHandler(HttpStatusCode.OK, events) { ContentType = "Text/Event-Stream" };
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);

    await client.ListToolsAsync("access-1", TestContext.Current.CancellationToken);

    using var reply = JsonDocument.Parse(Assert.IsType<string>(handler.ServerReply));
    Assert.Equal(17, reply.RootElement.GetProperty("id").GetInt32());
    Assert.Equal(JsonValueKind.Object, reply.RootElement.GetProperty("result").ValueKind);
    Assert.Equal("test-session", handler.SessionId);
    Assert.Equal("2025-06-18", handler.ProtocolVersion);
  }

  [Fact]
  public async Task InitializationSsePingUsesNewSessionHeader()
  {
    var initEvents = "data: {\"jsonrpc\":\"2.0\",\"id\":17,\"method\":\"ping\"}\n\n" +
        "data: {\"jsonrpc\":\"2.0\",\"id\":\"1\",\"result\":{\"protocolVersion\":\"2025-06-18\"}}\n\n";
    var handler = new StubHandler(HttpStatusCode.OK,
        """{"jsonrpc":"2.0","id":"2","result":{"tools":[]}}""")
    {
      InitializeEvents = initEvents
    };
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);

    await client.ListToolsAsync("access-1", TestContext.Current.CancellationToken);

    using var reply = JsonDocument.Parse(Assert.IsType<string>(handler.ServerReply));
    Assert.Equal(17, reply.RootElement.GetProperty("id").GetInt32());
    Assert.Equal("test-session", handler.ServerReplySession);
    Assert.Equal("2025-06-18", handler.ServerReplyProtocolVersion);
  }

  [Fact]
  public async Task AsyncDisposalTerminatesStatefulSession()
  {
    var handler = new StubHandler(HttpStatusCode.OK,
        """{"jsonrpc":"2.0","id":"2","result":{"tools":[]}}""")
    { ContentType = "Application/JSON" };
    var client = new MemberMcpClient(new Uri("https://example.test"), handler);
    await client.ListToolsAsync("access-1", TestContext.Current.CancellationToken);

    await client.DisposeAsync();

    Assert.Equal("DELETE", handler.Methods.Last());
    Assert.Equal("Bearer access-1", handler.Authorization);
    Assert.Equal("test-session", handler.SessionId);
    Assert.Equal("2025-06-18", handler.ProtocolVersion);
  }

  [Fact]
  public async Task CloseTerminatesSessionAndRejectsNewWork()
  {
    var handler = new StubHandler(HttpStatusCode.OK,
        """{"jsonrpc":"2.0","id":"2","result":{"tools":[]}}""");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);
    await client.ListToolsAsync("access-1", TestContext.Current.CancellationToken);

    await client.CloseAsync("access-1", TestContext.Current.CancellationToken);

    Assert.Equal("DELETE", handler.Methods.Last());
    await Assert.ThrowsAsync<ObjectDisposedException>(
        () => client.ListToolsAsync("access-1", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task StreamedBodyHasAnAbsoluteDeadlineAfterHeaders()
  {
    var stream = new SyntheticStream(stall: true);
    using var client = new MemberMcpClient(new Uri("https://example.test"),
        new StubHandler(HttpStatusCode.OK, "") { ToolStream = stream },
        responseTimeout: TimeSpan.FromMilliseconds(100));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => client.ListToolsAsync("access-1", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task UnterminatedSseLineStopsReadingAtSizeLimit()
  {
    var stream = new SyntheticStream(stall: false);
    using var client = new MemberMcpClient(new Uri("https://example.test"),
        new StubHandler(HttpStatusCode.OK, "") { ToolStream = stream });

    await Assert.ThrowsAsync<InvalidDataException>(
        () => client.ListToolsAsync("access-1", TestContext.Current.CancellationToken));
    Assert.InRange(stream.BytesRead, 1_100_001, 1_104_096);
  }

  [Theory]
  [InlineData("[]")]
  [InlineData("{\"jsonrpc\":2,\"id\":\"1\",\"result\":{}}")]
  [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}")]
  [InlineData("{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"error\":{\"code\":\"bad\"}}")]
  public async Task MalformedJsonRpcUsesInvalidDataContract(string body)
  {
    using var client = new MemberMcpClient(new Uri("https://example.test"),
        new StubHandler(HttpStatusCode.OK, body));
    await Assert.ThrowsAsync<InvalidDataException>(
        () => client.ListToolsAsync("access-1", TestContext.Current.CancellationToken));
  }

  private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
  {
    public string? Url { get; private set; }
    public string? Authorization { get; private set; }
    public string? Cookie { get; private set; }
    public string? Accept { get; private set; }
    public string? Body { get; private set; }
    public string? ProtocolVersion { get; private set; }
    public string? SessionId { get; private set; }
    public string? ServerReply { get; private set; }
    public string? ServerReplySession { get; private set; }
    public string? ServerReplyProtocolVersion { get; private set; }
    public string? InitializeEvents { get; init; }
    public List<string> Methods { get; } = [];
    public int? RetryAfterSeconds { get; init; }
    public DateTimeOffset? RetryAfterDate { get; init; }
    public string ContentType { get; init; } = "application/json";
    public SyntheticStream? ToolStream { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Url = request.RequestUri?.AbsoluteUri;
      Authorization = request.Headers.Authorization?.ToString();
      Cookie = request.Headers.TryGetValues("Cookie", out var cookies) ? cookies.Single() : null;
      Accept = string.Join(", ", request.Headers.Accept.Select(value => value.ToString()));
      if (request.Method == HttpMethod.Delete)
      {
        Methods.Add("DELETE");
        ProtocolVersion = request.Headers.TryGetValues("MCP-Protocol-Version", out var deleteVersions)
            ? deleteVersions.Single() : null;
        SessionId = request.Headers.TryGetValues("Mcp-Session-Id", out var deleteSessions)
            ? deleteSessions.Single() : null;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
      }
      Body = await request.Content!.ReadAsStringAsync(cancellationToken);
      using var rpc = JsonDocument.Parse(Body);
      if (!rpc.RootElement.TryGetProperty("method", out var methodValue))
      {
        ServerReply = Body;
        ProtocolVersion = request.Headers.TryGetValues("MCP-Protocol-Version", out var replyVersions)
            ? replyVersions.Single() : null;
        SessionId = request.Headers.TryGetValues("Mcp-Session-Id", out var replySessions)
            ? replySessions.Single() : null;
        ServerReplySession = SessionId;
        ServerReplyProtocolVersion = ProtocolVersion;
        return new HttpResponseMessage(HttpStatusCode.Accepted);
      }
      var method = methodValue.GetString()!;
      Methods.Add(method);
      if (method == "initialize")
      {
        var requestId = rpc.RootElement.GetProperty("id").GetString();
        var initialReply = """{"jsonrpc":"2.0","id":"1","result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{}},"serverInfo":{"name":"test","version":"1"}}}"""
            .Replace("\"id\":\"1\"", $"\"id\":\"{requestId}\"", StringComparison.Ordinal);
        var init = new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
              InitializeEvents ?? initialReply,
              Encoding.UTF8, InitializeEvents is null ? "application/json" : "text/event-stream")
        };
        init.Headers.TryAddWithoutValidation("Mcp-Session-Id", "test-session");
        return init;
      }
      ProtocolVersion = request.Headers.TryGetValues("MCP-Protocol-Version", out var versions) ? versions.Single() : null;
      SessionId = request.Headers.TryGetValues("Mcp-Session-Id", out var sessions) ? sessions.Single() : null;
      if (method == "notifications/initialized")
        return new HttpResponseMessage(HttpStatusCode.Accepted);
      var response = new HttpResponseMessage(status)
      {
        Content = ToolStream is null
            ? new StringContent(responseBody, Encoding.UTF8, ContentType)
            : new StreamContent(ToolStream)
      };
      if (ToolStream is not null)
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
      if (RetryAfterSeconds is { } seconds)
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
      if (RetryAfterDate is { } date)
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(date);
      return response;
    }
  }

  private sealed class SyntheticStream(bool stall) : Stream
  {
    public int BytesRead { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
      if (stall)
      {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
      }
      if (BytesRead >= 1_200_000) throw new IOException("The line reader exceeded its bounded budget.");
      var count = Math.Min(buffer.Length, 1_200_000 - BytesRead);
      buffer.Span[..count].Fill((byte)'x');
      BytesRead += count;
      return count;
    }
  }
}

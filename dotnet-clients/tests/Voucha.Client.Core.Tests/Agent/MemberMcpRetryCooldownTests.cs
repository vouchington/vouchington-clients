using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpRetryCooldownTests
{
  [Fact]
  public async Task FirstRefusalRetainsExactServerDelayWhenTimestampAdvances()
  {
    var clock = new RetryClock { TimestampAdvance = TimeSpan.FromTicks(1) };
    var handler = new RetryHandler(clock, "seconds", "tools/list");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler, timeProvider: clock);
    var error = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    Assert.Equal(TimeSpan.FromSeconds(17), error.RetryAfter);
  }

  [Theory]
  [InlineData("seconds", "tools/list")]
  [InlineData("date", "tools/list")]
  [InlineData("in-band", "tools/call")]
  [InlineData("seconds", "initialize")]
  [InlineData("seconds", "notifications/initialized")]
  public async Task RefusalStopsDispatchUntilMonotonicDeadlineAndNeverRedispatches(string mode, string method)
  {
    var clock = new RetryClock();
    var handler = new RetryHandler(clock, mode, method);
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler, timeProvider: clock);
    var first = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, method));
    Assert.Equal(TimeSpan.FromSeconds(17), first.RetryAfter);
    var admitted = handler.Requests;

    clock.JumpWallClock(TimeSpan.FromDays(365));
    clock.Advance(TimeSpan.FromSeconds(10));
    var blocked = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, method));
    Assert.Equal(TimeSpan.FromSeconds(7), blocked.RetryAfter);
    Assert.Equal(admitted, handler.Requests);
    clock.Advance(TimeSpan.FromSeconds(7));
    Assert.Equal(admitted, handler.Requests);

    await Invoke(client, method);
    Assert.True(handler.Requests > admitted);
    Assert.Equal(method == "tools/call" ? 2 : 0, handler.ToolCalls);
  }

  [Fact]
  public async Task ServerPingReplyRefusalBlocksNextUserRequestUntilExplicitExpiry()
  {
    var clock = new RetryClock();
    var handler = new RetryHandler(clock, "server-reply", "tools/list");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler, timeProvider: clock);
    var first = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    Assert.Equal(TimeSpan.FromSeconds(17), first.RetryAfter);
    Assert.Equal(1, handler.Replies);
    var admitted = handler.Requests;
    var blocked = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    Assert.Equal(TimeSpan.FromSeconds(17), blocked.RetryAfter);
    Assert.Equal(admitted, handler.Requests);
    var replacementHandler = new RetryHandler(clock, "none", "tools/list");
    using var replacement = new MemberMcpClient(new Uri("https://example.test"), replacementHandler, timeProvider: clock);
    await replacement.ListToolsAsync("replacement-account", TestContext.Current.CancellationToken);
    Assert.Equal(3, replacementHandler.Requests);
    Assert.Equal(admitted, handler.Requests);
    clock.Advance(TimeSpan.FromSeconds(17));
    Assert.Equal(admitted, handler.Requests);
    await Invoke(client, "tools/list");
    Assert.Equal(admitted + 1, handler.Requests);
    Assert.Equal(1, handler.Replies);
  }

  [Fact]
  public async Task AlreadyInFlightPingCannotPostReplyDuringCooldown()
  {
    var clock = new RetryClock();
    var handler = new RetryHandler(clock, "held", "tools/list");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler, timeProvider: clock);
    var first = Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    await handler.WaitForHeldAsync(1);
    var second = Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    await handler.WaitForHeldAsync(2);
    handler.Release(0, 17);
    await first;
    var admitted = handler.Requests;
    handler.ReleasePing(1);
    var replyRefusal = await second;
    Assert.Equal(TimeSpan.FromSeconds(17), replyRefusal.RetryAfter);
    Assert.Equal(admitted, handler.Requests);
    Assert.Equal(0, handler.Replies);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("invalid")]
  [InlineData("0")]
  [InlineData("-1")]
  public async Task MissingOrInvalidDelayDoesNotInventACooldown(string? retryAfter)
  {
    var clock = new RetryClock();
    var handler = new RetryHandler(clock, "unknown", "tools/list") { UnknownDelay = retryAfter };
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler, timeProvider: clock);
    var failure = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    Assert.Null(failure.RetryAfter);
    var admitted = handler.Requests;
    await Invoke(client, "tools/list");
    Assert.Equal(admitted + 1, handler.Requests);
  }

  [Fact]
  public async Task CancellationAndCloseRemainAvailableAndNewConnectionDoesNotInheritCooldown()
  {
    var clock = new RetryClock();
    var handler = new RetryHandler(clock, "seconds", "tools/list");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler, timeProvider: clock);
    await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();
    var admitted = handler.Requests;
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListToolsAsync("old-account", cancellation.Token));
    Assert.Equal(admitted, handler.Requests);
    var stillLimited = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(client, "tools/list"));
    Assert.Equal(TimeSpan.FromSeconds(17), stillLimited.RetryAfter);
    Assert.Equal(admitted, handler.Requests);
    await client.CloseAsync("old-account", TestContext.Current.CancellationToken);
    Assert.Equal(1, handler.Deletes);
    await Assert.ThrowsAsync<ObjectDisposedException>(() => Invoke(client, "tools/list"));

    var replacementHandler = new RetryHandler(clock, "none", "tools/list");
    using var replacement = new MemberMcpClient(new Uri("https://example.test"), replacementHandler, timeProvider: clock);
    await replacement.ListToolsAsync("new-account", TestContext.Current.CancellationToken);
    Assert.Equal(3, replacementHandler.Requests);
  }

  [Fact]
  public async Task LateShorterRefusalCannotShortenDeadlineOrThrottleReplacement()
  {
    var clock = new RetryClock();
    var handler = new RetryHandler(clock, "held", "tools/list");
    using var original = new MemberMcpClient(new Uri("https://example.test"), handler, timeProvider: clock);
    var first = Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(original, "tools/list"));
    await handler.WaitForHeldAsync(1);
    var second = Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(original, "tools/list"));
    await handler.WaitForHeldAsync(2);
    handler.Release(0, 17);
    await first;
    clock.Advance(TimeSpan.FromSeconds(10));
    var replacementHandler = new RetryHandler(clock, "none", "tools/list");
    using var replacement = new MemberMcpClient(new Uri("https://example.test"), replacementHandler, timeProvider: clock);
    await Invoke(replacement, "tools/list");
    handler.Release(1, 2);
    var lateRefusal = await second;
    Assert.Equal(TimeSpan.FromSeconds(7), lateRefusal.RetryAfter);
    var admitted = handler.Requests;
    var blocked = await Assert.ThrowsAsync<McpRateLimitException>(() => Invoke(original, "tools/list"));
    Assert.Equal(TimeSpan.FromSeconds(7), blocked.RetryAfter);
    Assert.Equal(admitted, handler.Requests);
    await Invoke(replacement, "tools/list");
    Assert.Equal(4, replacementHandler.Requests);
  }

  private static async Task Invoke(MemberMcpClient client, string method)
  {
    if (method == "tools/call")
      _ = await client.CallToolAsync("manage_bookmark", JsonSerializer.SerializeToElement(new { }),
          "account-access", TestContext.Current.CancellationToken);
    else _ = await client.ListToolsAsync("account-access", TestContext.Current.CancellationToken);
  }

  private sealed class RetryClock : TimeProvider
  {
    private long ticks;
    private long wallTicks;
    public TimeSpan TimestampAdvance { get; init; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Add(ref ticks, TimestampAdvance.Ticks);
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero)
        .AddTicks(Interlocked.Read(ref wallTicks));
    public void Advance(TimeSpan value) => Interlocked.Add(ref ticks, value.Ticks);
    public void JumpWallClock(TimeSpan value) => Interlocked.Add(ref wallTicks, value.Ticks);
  }

  private sealed class RetryHandler(RetryClock clock, string mode, string limitedMethod) : HttpMessageHandler
  {
    private readonly object gate = new();
    private readonly List<TaskCompletionSource<HttpResponseMessage>> held = [];
    private readonly TaskCompletionSource firstHeld = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource secondHeld = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int requests;
    private int failures;
    private int toolCalls;
    private int deletes;
    private int replies;
    public int Requests => Volatile.Read(ref requests);
    public int ToolCalls => Volatile.Read(ref toolCalls);
    public int Replies => Volatile.Read(ref replies);
    public int Deletes => Volatile.Read(ref deletes);
    public string? UnknownDelay { get; init; }

    public Task WaitForHeldAsync(int count) => (count == 1 ? firstHeld.Task : secondHeld.Task)
        .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    public void Release(int index, int seconds)
    {
      TaskCompletionSource<HttpResponseMessage> completion;
      lock (gate) completion = held[index];
      var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
      response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
      completion.SetResult(response);
    }

    public void ReleasePing(int index)
    {
      TaskCompletionSource<HttpResponseMessage> completion;
      lock (gate) completion = held[index];
      completion.SetResult(Ping());
    }

    private static HttpResponseMessage Ping() => new(HttpStatusCode.OK)
    {
      Content = new StringContent("data: {\"jsonrpc\":\"2.0\",\"id\":\"server-ping\",\"method\":\"ping\"}\n\n",
          Encoding.UTF8, "text/event-stream")
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref requests);
      if (request.Method == HttpMethod.Delete)
      {
        Interlocked.Increment(ref deletes);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
      }
      using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
      var method = body.RootElement.TryGetProperty("method", out var methodValue) ? methodValue.GetString() : null;
      if (mode == "server-reply" && method is null)
      {
        Assert.Equal("server-ping", body.RootElement.GetProperty("id").GetString());
        Assert.True(body.RootElement.TryGetProperty("result", out _));
        Interlocked.Increment(ref replies);
        var refused = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
        return refused;
      }
      if (mode == "server-reply" && method == "tools/list" && Volatile.Read(ref replies) == 0)
        return Ping();
      if (method == "tools/call") Interlocked.Increment(ref toolCalls);
      if (mode == "held" && method == limitedMethod)
      {
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
          held.Add(completion);
          if (held.Count == 1) firstHeld.SetResult();
          if (held.Count == 2) secondHeld.SetResult();
        }
        return await completion.Task.WaitAsync(cancellationToken);
      }
      if (mode is not ("none" or "server-reply") && method == limitedMethod && Interlocked.Increment(ref failures) == 1)
      {
        if (mode == "in-band")
          return Reply(body.RootElement, new
          {
            isError = true,
            content = new[]
          {
            new { type = "text", text = """{"error":{"code":"RATE_LIMIT","retryAfterSeconds":17}}""" },
          }
          });
        var refused = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (mode == "seconds") refused.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
        else if (mode == "date") refused.Headers.RetryAfter = new RetryConditionHeaderValue(clock.GetUtcNow().AddSeconds(17));
        else if (UnknownDelay is not null) refused.Headers.TryAddWithoutValidation("Retry-After", UnknownDelay);
        return refused;
      }
      if (method == "notifications/initialized") return new HttpResponseMessage(HttpStatusCode.Accepted);
      return method == "initialize"
          ? Reply(body.RootElement, new { protocolVersion = "2025-06-18" })
          : Reply(body.RootElement, new { tools = Array.Empty<object>(), content = Array.Empty<object>(), isError = false });
    }

    private static HttpResponseMessage Reply(JsonElement request, object result)
    {
      var response = new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
          jsonrpc = "2.0",
          id = request.GetProperty("id").GetString(),
          result,
        }), Encoding.UTF8, "application/json"),
      };
      response.Headers.TryAddWithoutValidation("Mcp-Session-Id", "retry-session");
      return response;
    }
  }
}

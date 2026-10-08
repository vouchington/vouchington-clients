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
        """{"jsonrpc":"2.0","id":"1","result":{"content":[{"type":"text","text":"<untrusted>member text</untrusted>"}],"structuredContent":{"post_id":"post-1"},"isError":false}}""");
    using var client = new MemberMcpClient(new Uri("https://example.test"), handler);
    var arguments = JsonDocument.Parse("""{"id":"post-1"}""").RootElement;

    var result = await client.CallToolAsync("read_post", arguments, "access-1", TestContext.Current.CancellationToken);

    Assert.Equal("https://example.test/api/v1/mcp", handler.Url);
    Assert.Equal("Bearer access-1", handler.Authorization);
    Assert.Null(handler.Cookie);
    Assert.Equal("application/json, text/event-stream", handler.Accept);
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
  public async Task InBandRateLimitRetainsRetryAfter()
  {
    var inner = """{"error":{"status":429,"code":"RATE_LIMIT","retryAfterSeconds":17}}""";
    var body = JsonSerializer.Serialize(new
    {
      jsonrpc = "2.0",
      id = "1",
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
    var malformed = """{"jsonrpc":"2.0","id":"1","result":{"isError":true,"content":[{"type":"text","text":"{\"error\":[]}"}]}}""";
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

  private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
  {
    public string? Url { get; private set; }
    public string? Authorization { get; private set; }
    public string? Cookie { get; private set; }
    public string? Accept { get; private set; }
    public string? Body { get; private set; }
    public int? RetryAfterSeconds { get; init; }
    public DateTimeOffset? RetryAfterDate { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Url = request.RequestUri?.AbsoluteUri;
      Authorization = request.Headers.Authorization?.ToString();
      Cookie = request.Headers.TryGetValues("Cookie", out var cookies) ? cookies.Single() : null;
      Accept = string.Join(", ", request.Headers.Accept.Select(value => value.ToString()));
      Body = await request.Content!.ReadAsStringAsync(cancellationToken);
      var response = new HttpResponseMessage(status)
      {
        Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
      };
      if (RetryAfterSeconds is { } seconds)
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
      if (RetryAfterDate is { } date)
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(date);
      return response;
    }
  }
}

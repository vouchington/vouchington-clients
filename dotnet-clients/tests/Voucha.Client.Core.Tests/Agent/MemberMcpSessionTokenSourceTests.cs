using System.Net;
using System.Text;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpSessionTokenSourceTests
{
  private static readonly Uri Resource = new("https://api.test/api/v1/mcp");

  [Fact]
  public async Task MintsOnceWithNativeHeadersAndNoBrowserMetadata()
  {
    var handler = new MintHandler();
    using var source = Source(handler);

    var first = await source.AccessTokenAsync(TestContext.Current.CancellationToken);
    var second = await source.AccessTokenAsync(TestContext.Current.CancellationToken);

    Assert.Equal("token-1", first);
    Assert.Equal("token-1", second);
    var request = Assert.Single(handler.Requests);
    Assert.Equal("/api/v1/oauth/native-session-tokens", request.Path);
    Assert.Equal("1", request.Headers["x-voucha-native-mcp-mint"]);
    Assert.Equal("""{"client_app":"windows"}""", request.Body);
    Assert.DoesNotContain(request.Headers.Keys, name =>
        name.Equals("Origin", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Sec-Fetch-", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public async Task MintsAgainAfterExpiry()
  {
    var clock = new TestClock();
    var handler = new MintHandler();
    using var source = Source(handler, clock);

    Assert.Equal("token-1", await source.AccessTokenAsync(TestContext.Current.CancellationToken));
    clock.Advance(TimeSpan.FromSeconds(3600));

    Assert.Equal("token-2", await source.AccessTokenAsync(TestContext.Current.CancellationToken));
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task ConcurrentCallersShareOneMint()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handler = new MintHandler { Hold = gate.Task };
    using var source = Source(handler);

    var calls = new[] { source.AccessTokenAsync(TestContext.Current.CancellationToken),
        source.AccessTokenAsync(TestContext.Current.CancellationToken) };
    gate.SetResult();

    Assert.Equal(["token-1", "token-1"], await Task.WhenAll(calls));
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task RejectedTokenIsReplacedButNewerTokenIsReused()
  {
    var handler = new MintHandler();
    using var source = Source(handler);
    var first = await source.AccessTokenAsync(TestContext.Current.CancellationToken);

    var second = await source.RefreshIfCurrentAsync(first, TestContext.Current.CancellationToken);
    var reused = await source.RefreshIfCurrentAsync(first, TestContext.Current.CancellationToken);

    Assert.Equal("token-2", second);
    Assert.Equal("token-2", reused);
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task ClearDropsTheTokenAndFencesAMintInFlight()
  {
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handler = new MintHandler { Hold = gate.Task };
    using var source = Source(handler);
    var pending = source.AccessTokenAsync(TestContext.Current.CancellationToken);

    source.Clear();
    gate.SetResult();

    await Assert.ThrowsAsync<McpUnauthorizedException>(() => pending);
    await Assert.ThrowsAsync<McpUnauthorizedException>(
        () => source.AccessTokenAsync(TestContext.Current.CancellationToken));
    Assert.Null(source.CurrentAccessToken());
  }

  [Fact]
  public async Task AccountChangeDuringMintDiscardsTheToken()
  {
    var current = true;
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var handler = new MintHandler { Hold = gate.Task };
    using var source = Source(handler, stillCurrent: () => current);
    var pending = source.AccessTokenAsync(TestContext.Current.CancellationToken);

    current = false;
    gate.SetResult();

    await Assert.ThrowsAsync<McpUnauthorizedException>(() => pending);
    Assert.Null(source.CurrentAccessToken());
    await Assert.ThrowsAsync<McpUnauthorizedException>(
        () => source.AccessTokenAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task UnauthorizedMintIsUnauthorizedAndForbiddenIsSurfaced()
  {
    using var expired = Source(new MintHandler { Status = HttpStatusCode.Unauthorized });
    await Assert.ThrowsAsync<McpUnauthorizedException>(
        () => expired.AccessTokenAsync(TestContext.Current.CancellationToken));

    using var gated = Source(new MintHandler { Status = HttpStatusCode.Forbidden });
    var error = await Assert.ThrowsAsync<VouchaApiException>(
        () => gated.AccessTokenAsync(TestContext.Current.CancellationToken));
    Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
  }

  [Theory]
  [InlineData("https://other.test/api/v1/mcp", "Bearer", 3600)]
  [InlineData("https://api.test/api/v1/mcp", "MAC", 3600)]
  [InlineData("https://api.test/api/v1/mcp", "Bearer", 0)]
  public async Task MismatchedMintResponseIsRejected(string resource, string tokenType, int expiresIn)
  {
    using var source = Source(new MintHandler { ResponseResource = resource, TokenType = tokenType, ExpiresIn = expiresIn });

    await Assert.ThrowsAsync<InvalidDataException>(
        () => source.AccessTokenAsync(TestContext.Current.CancellationToken));
  }

  private static MemberMcpSessionTokenSource Source(
      MintHandler handler, TestClock? clock = null, Func<bool>? stillCurrent = null) =>
      new(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
          Resource, "windows", stillCurrent, clock);

  internal sealed class TestClock : TimeProvider
  {
    private DateTimeOffset now = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan by) => now += by;
  }

  internal sealed record Seen(string Path, string Body, Dictionary<string, string> Headers);

  internal sealed class MintHandler : HttpMessageHandler
  {
    public List<Seen> Requests { get; } = [];
    public Task? Hold { get; init; }
    public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
    public string ResponseResource { get; init; } = "https://api.test/api/v1/mcp";
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; } = 3600;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
      Requests.Add(new Seen(request.RequestUri!.AbsolutePath,
          await request.Content!.ReadAsStringAsync(cancellationToken), headers));
      if (Hold is not null) await Hold.WaitAsync(cancellationToken);
      var json = Status == HttpStatusCode.OK
          ? $$"""{"access_token":"token-{{Requests.Count}}","expires_in":{{ExpiresIn}},"resource":"{{ResponseResource}}","scope":"mcp.user:read","token_type":"{{TokenType}}"}"""
          : """{"error":"denied"}""";
      return new HttpResponseMessage(Status)
      {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }
  }
}

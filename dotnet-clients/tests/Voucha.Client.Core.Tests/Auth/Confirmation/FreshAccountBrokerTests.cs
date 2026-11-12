using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class FreshAccountBrokerTests
{
  [Fact]
  public async Task ReusesNativeCallbackProtocolAndS256HandoffWithoutRefreshingSession()
  {
    var handler = new BrokerHandler();
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser);
    var proof = broker.VerifyAsync("github", TestContext.Current.CancellationToken);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.False(await broker.HandleCallbackAsync(new Uri("voucha://auth/oauth/callback?flow_id=another&completion_token=x")));
    Assert.True(await broker.HandleCallbackAsync(Callback));
    Assert.Equal("broker-proof", await proof);
    Assert.False(await broker.HandleCallbackAsync(Callback));
    Assert.Equal(3, handler.Requests.Count);
    using var begin = JsonDocument.Parse(handler.Requests[0].Body!);
    using var complete = JsonDocument.Parse(handler.Requests[1].Body!);
    Assert.Equal("native", begin.RootElement.GetProperty("callback_mode").GetString());
    var verifier = complete.RootElement.GetProperty("completion_proof_verifier").GetString()!;
    Assert.Equal(NativeAuthorizationCompletionProof.FromVerifier(verifier).Challenge,
        begin.RootElement.GetProperty("completion_proof_challenge").GetString());
    Assert.Equal("callback-token", complete.RootElement.GetProperty("completion_token").GetString());
    Assert.EndsWith("/github/proofs", handler.Requests[2].Path, StringComparison.Ordinal);
    Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/session", StringComparison.Ordinal));
  }

  [Fact]
  public async Task CancellationRejectsLateCallbackBeforeAnyCompletionOrProofRetrieval()
  {
    var handler = new BrokerHandler();
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var proof = broker.VerifyAsync("github", cancellation.Token);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    await cancellation.CancelAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => proof);
    Assert.False(await broker.HandleCallbackAsync(Callback));
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData(HttpStatusCode.Unauthorized)]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.Gone)]
  public async Task NonceSubjectOrExpiredProviderRejectionDoesNotRetryOrReturnProof(HttpStatusCode status)
  {
    var handler = new BrokerHandler { CompletionStatus = status };
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser);
    var proof = broker.VerifyAsync("github", TestContext.Current.CancellationToken);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(await broker.HandleCallbackAsync(Callback));
    await Assert.ThrowsAsync<VouchaApiException>(() => proof);
    Assert.Equal(2, handler.Requests.Count);
    Assert.DoesNotContain(handler.Requests, request => request.Path.EndsWith("/proofs", StringComparison.Ordinal));
  }

  [Fact]
  public async Task GoogleDoesNotFallBackToBrokerReauthentication()
  {
    var handler = new BrokerHandler();
    var broker = new FreshAccountBroker(Client(handler), new Browser());
    await Assert.ThrowsAsync<NotSupportedException>(() => broker.VerifyAsync("google", TestContext.Current.CancellationToken));
    Assert.Empty(handler.Requests);
  }

  private static readonly Uri Callback = new("voucha://auth/oauth/callback?flow_id=flow-1&completion_token=callback-token");
  private static VouchaApiClient Client(BrokerHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private sealed class Browser : INativeExternalBrowser
  {
    public TaskCompletionSource<Uri> Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    { Opened.TrySetResult(uri); return Task.FromResult(true); }
  }

  private sealed class BrokerHandler : HttpMessageHandler
  {
    public HttpStatusCode CompletionStatus { get; init; } = HttpStatusCode.OK;
    public List<(string Path, string? Body)> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      Requests.Add((path, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
      var status = path.EndsWith("/complete", StringComparison.Ordinal) ? CompletionStatus : HttpStatusCode.OK;
      object body = path.EndsWith("/authorizations", StringComparison.Ordinal)
          ? new { flow_id = "flow-1", redirect_url = "https://github.com/authorize", expires_at = DateTimeOffset.UtcNow.AddMinutes(1) }
          : path.EndsWith("/complete", StringComparison.Ordinal)
              ? new { oauth_account = new { id = "linked", name = "Account", email_address = "a@example.test" } }
              : new { re_auth_token = "broker-proof" };
      return new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(body)), RequestMessage = request };
    }
  }
}

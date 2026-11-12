using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Tests.Support;
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
    Assert.False(await broker.HandleCallbackAsync(new Uri("voucha://auth/oauth/callback?flow_id=another&completion_token=x"), TestContext.Current.CancellationToken));
    Assert.True(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
    Assert.Equal("broker-proof", await proof);
    Assert.False(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
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
    Assert.False(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
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
    Assert.True(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
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

  [Theory]
  [InlineData("", "https://github.com/authorize", false)]
  [InlineData("flow-1", "http://github.com/authorize", false)]
  [InlineData("flow-1", "https://github.com/authorize", true)]
  public async Task InvalidAuthorizationNeverOpensBrowser(string flowId, string redirect, bool expired)
  {
    var handler = new BrokerHandler { FlowId = flowId, Redirect = redirect, Expired = expired };
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser);
    await Assert.ThrowsAsync<InvalidOperationException>(() => broker.VerifyAsync("github", TestContext.Current.CancellationToken));
    Assert.False(browser.Opened.Task.IsCompleted);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task DeclinedBrowserCancelsProofAndRejectsLaterCallback()
  {
    var handler = new BrokerHandler();
    var broker = new FreshAccountBroker(Client(handler), new Browser { OpenResult = false });
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => broker.VerifyAsync("github", TestContext.Current.CancellationToken));
    Assert.False(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task MatchingMalformedAndDuplicateCallbacksNeverDuplicateCompletionRequests()
  {
    var handler = new BrokerHandler { HeldCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser);
    var proof = broker.VerifyAsync("github", TestContext.Current.CancellationToken);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    await Assert.ThrowsAsync<InvalidOperationException>(() => broker.VerifyAsync("github", TestContext.Current.CancellationToken));
    Assert.False(await broker.HandleCallbackAsync(new Uri("https://auth/oauth/callback?flow_id=flow-1"), TestContext.Current.CancellationToken));
    Assert.False(await broker.HandleCallbackAsync(new Uri("voucha://auth/oauth/callback"), TestContext.Current.CancellationToken));
    Assert.True(await broker.HandleCallbackAsync(new Uri("voucha://auth/oauth/callback?flow_id=flow-1"), TestContext.Current.CancellationToken));
    var callback = broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken);
    await handler.CompletionStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
    handler.HeldCompletion.SetResult();
    Assert.True(await callback);
    Assert.Equal("broker-proof", await proof);
    Assert.Equal(3, handler.Requests.Count);
  }

  [Fact]
  public async Task PendingBrokerResponsePollsOnlyAfterVirtualDelayThenReturnsFreshProof()
  {
    var clock = new ConfirmationClock();
    var handler = new BrokerHandler { PendingResponses = 1 };
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser, clock);
    var proof = broker.VerifyAsync("github", TestContext.Current.CancellationToken);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    var callback = broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken);
    await clock.PollDelayCreated.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, handler.Requests.Count);
    Assert.False(proof.IsCompleted);
    clock.Advance(TimeSpan.FromSeconds(1));
    Assert.True(await callback);
    Assert.Equal("broker-proof", await proof);
    Assert.Equal(4, handler.Requests.Count);
  }

  [Fact]
  public async Task ExpiredFlowRejectsCallbackWithoutCompletionEvenBeforeTimerDispatch()
  {
    var clock = new ConfirmationClock();
    var handler = new BrokerHandler();
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser, clock);
    var proof = broker.VerifyAsync("github", TestContext.Current.CancellationToken);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    clock.Jump(TimeSpan.FromMinutes(2));
    Assert.True(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => proof);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task CancellationDuringPendingPollCannotFetchProof()
  {
    var clock = new ConfirmationClock();
    var handler = new BrokerHandler { PendingResponses = 1 };
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser, clock);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var proof = broker.VerifyAsync("github", cancellation.Token);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    var callback = broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken);
    await clock.PollDelayCreated.Task.WaitAsync(TestContext.Current.CancellationToken);
    await cancellation.CancelAsync();
    Assert.True(await callback);
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => proof);
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task LoginMfaOutcomeCannotBePromotedIntoAccountChangeProof()
  {
    var handler = new BrokerHandler { CompletionJson = """{"mfa_required":true,"login_attempt_id":"login-attempt"}""" };
    var browser = new Browser();
    var broker = new FreshAccountBroker(Client(handler), browser);
    var proof = broker.VerifyAsync("github", TestContext.Current.CancellationToken);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(await broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<InvalidOperationException>(() => proof);
    Assert.Equal(2, handler.Requests.Count);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task BrowserDeclineDuringCallbackCannotRetrieveProofEvenWhenServerLaterCompletes(bool pendingResponse)
  {
    var clock = new ConfirmationClock();
    var handler = new BrokerHandler
    {
      HeldCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously),
      PendingResponses = pendingResponse ? 1 : 0,
    };
    var browser = new Browser { DeferredResult = new(TaskCreationOptions.RunContinuationsAsynchronously) };
    var broker = new FreshAccountBroker(Client(handler), browser, clock);
    var proof = broker.VerifyAsync("github", TestContext.Current.CancellationToken);
    await browser.Opened.Task.WaitAsync(TestContext.Current.CancellationToken);
    var callback = broker.HandleCallbackAsync(Callback, TestContext.Current.CancellationToken);
    await handler.CompletionStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    browser.DeferredResult.SetResult(false);
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => proof);
    handler.HeldCompletion.SetResult();
    if (pendingResponse)
    {
      await clock.PollDelayCreated.Task.WaitAsync(TestContext.Current.CancellationToken);
      clock.Advance(TimeSpan.FromSeconds(1));
    }
    Assert.True(await callback);
    Assert.Equal(2, handler.Requests.Count);
  }

  private static readonly Uri Callback = new("voucha://auth/oauth/callback?flow_id=flow-1&completion_token=callback-token");
  private static VouchaApiClient Client(BrokerHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private sealed class Browser : INativeExternalBrowser
  {
    public bool OpenResult { get; init; } = true;
    public TaskCompletionSource<bool>? DeferredResult { get; init; }
    public TaskCompletionSource<Uri> Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    { Opened.TrySetResult(uri); return DeferredResult?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(OpenResult); }
  }

  private sealed class BrokerHandler : HttpMessageHandler
  {
    public string FlowId { get; init; } = "flow-1";
    public string Redirect { get; init; } = "https://github.com/authorize";
    public bool Expired { get; init; }
    public int PendingResponses { get; set; }
    public string? CompletionJson { get; init; }
    public TaskCompletionSource? HeldCompletion { get; init; }
    public TaskCompletionSource CompletionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public HttpStatusCode CompletionStatus { get; init; } = HttpStatusCode.OK;
    public List<(string Path, string? Body)> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri!.AbsolutePath;
      Requests.Add((path, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
      if (path.EndsWith("/complete", StringComparison.Ordinal))
      {
        CompletionStarted.TrySetResult();
        if (HeldCompletion is not null) await HeldCompletion.Task.WaitAsync(cancellationToken);
        if (CompletionJson is not null)
          return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CompletionJson), RequestMessage = request };
        if (PendingResponses-- > 0)
          return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("""{"status":"pending"}"""), RequestMessage = request };
      }
      var status = path.EndsWith("/complete", StringComparison.Ordinal) ? CompletionStatus : HttpStatusCode.OK;
      object body = path.EndsWith("/authorizations", StringComparison.Ordinal)
          ? new { flow_id = FlowId, redirect_url = Redirect, expires_at = DateTimeOffset.UtcNow.AddMinutes(Expired ? -1 : 1) }
          : path.EndsWith("/complete", StringComparison.Ordinal)
              ? new { oauth_account = new { id = "linked", name = "Account", email_address = "a@example.test" } }
              : new { re_auth_token = "broker-proof" };
      return new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(body)), RequestMessage = request };
    }
  }
}

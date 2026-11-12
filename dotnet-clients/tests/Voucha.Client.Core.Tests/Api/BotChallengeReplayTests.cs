using System.Net;
using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class BotChallengeReplayTests
{
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task ExactChallengeReplaysOriginalMutationBytesOnce(bool typed)
  {
    var session = new Session();
    var body = new Dictionary<string, string> { ["value"] = "original" };
    var headers = new Dictionary<string, string> { ["Idempotency-Key"] = "original-key" };
    var prompts = 0;
    var handler = new Handler();
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var client = new VouchaApiClient(http, new BotChallengeReplay(() => session, (action, _) =>
    {
      Assert.Equal("bot-challenge.mutation", action);
      prompts++;
      body["value"] = "changed";
      headers["Idempotency-Key"] = "changed-key";
      return Task.FromResult("fresh-proof");
    }));
    var request = Mutation() with { Body = body, Headers = headers };

    if (typed)
    {
      var response = await client.SendAsync<Ok>(request, TestContext.Current.CancellationToken);
      Assert.True(response.Success);
    }
    else await client.SendAsync(request, TestContext.Current.CancellationToken);

    Assert.Equal(1, prompts);
    Assert.Equal(2, handler.Requests.Count);
    var first = handler.Requests[0];
    var replay = handler.Requests[1];
    Assert.Equal("PATCH", replay.Method);
    Assert.Equal("https://api.test/api/v1/items/item?query=two%20words", replay.Uri);
    Assert.Equal(first.Body, replay.Body);
    Assert.Contains("original", replay.Body!, StringComparison.Ordinal);
    Assert.Equal("original-key", replay.IdempotencyKey);
    Assert.Null(first.Proof);
    Assert.Equal("fresh-proof", replay.Proof);
    Assert.Equal(0, session.Subscribers);
  }

  [Theory]
  [InlineData("ordinary")]
  [InlineData("wrong-action")]
  [InlineData("bad-json")]
  [InlineData("network")]
  [InlineData("wrong-status")]
  [InlineData("get")]
  [InlineData("other-origin")]
  public async Task OtherOutcomesNeverPromptOrReplay(string outcome)
  {
    var handler = new Handler { Outcome = outcome };
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var prompts = 0;
    var client = new VouchaApiClient(http, new BotChallengeReplay(() => new Session(), (_, _) =>
    {
      prompts++;
      return Task.FromResult("proof");
    }));
    var request = outcome == "get" ? new ApiRequest(HttpMethod.Get, "/api/v1/items/item") : Mutation();
    if (outcome == "other-origin") request = new ApiRequest(HttpMethod.Post, "https://untrusted.test/api/v1/items");
    await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.SendAsync(request, TestContext.Current.CancellationToken));
    Assert.Equal(0, prompts);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task RepeatedChallengeIsReturnedWithoutASecondPrompt()
  {
    var handler = new Handler { Outcome = "repeat" };
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var prompts = 0;
    var client = new VouchaApiClient(http, new BotChallengeReplay(() => new Session(), (_, _) =>
    {
      prompts++;
      return Task.FromResult("proof");
    }));
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => client.SendAsync(Mutation(), TestContext.Current.CancellationToken));
    Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    Assert.Equal(1, prompts);
    Assert.Equal(2, handler.Requests.Count);
  }

  [Theory]
  [InlineData("blank")]
  [InlineData("cancel")]
  [InlineData("failure")]
  [InlineData("account")]
  [InlineData("signout")]
  [InlineData("same-account-login")]
  public async Task UnsuccessfulProofNeverReplays(string outcome)
  {
    var session = new Session();
    var handler = new Handler();
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var client = new VouchaApiClient(http, new BotChallengeReplay(() => session, (_, _) =>
    {
      if (outcome == "cancel") cancellation.Cancel();
      if (outcome == "account") { session.Set("other"); session.Set("account"); }
      if (outcome == "signout") session.Set(null);
      if (outcome == "same-account-login") { session.Set(null); session.Set("account"); }
      if (outcome == "failure") throw new InvalidOperationException("proof failed");
      return Task.FromResult(outcome == "blank" ? " " : "late-proof");
    }));
    var error = await Record.ExceptionAsync(() => client.SendAsync(Mutation(), cancellation.Token));
    Assert.NotNull(error);
    if (outcome == "blank") Assert.IsType<VouchaApiException>(error);
    else if (outcome == "failure") Assert.IsType<InvalidOperationException>(error);
    else Assert.IsAssignableFrom<OperationCanceledException>(error);
    Assert.Single(handler.Requests);
    Assert.Equal(0, session.Subscribers);
  }

  [Fact]
  public async Task AccountChangeCancelsPendingProofEvenWhenProviderIgnoresCancellation()
  {
    var session = new Session();
    var handler = new Handler();
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var proof = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var client = new VouchaApiClient(http, new BotChallengeReplay(() => session, (_, _) =>
    {
      entered.SetResult();
      return proof.Task;
    }));
    var send = client.SendAsync(Mutation(), TestContext.Current.CancellationToken);
    await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
    session.Set("another");
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send.WaitAsync(TestContext.Current.CancellationToken));
    proof.SetResult("stale");
    Assert.Single(handler.Requests);
    Assert.Equal(0, session.Subscribers);
  }

  [Fact]
  public async Task SuccessfulMutationsAndAnalyticsNeverPrompt()
  {
    var session = new Session();
    var handler = new Handler { Outcome = "success" };
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var client = new VouchaApiClient(http, new BotChallengeReplay(() => session, (_, _) =>
        throw new InvalidOperationException("Unexpected prompt")));
    await client.SendAsync(Mutation(), TestContext.Current.CancellationToken);
    handler.Outcome = "repeat";
    var analytics = new SessionProductSurfaceAnalytics(client, session);
    analytics.RecordLandingPageVisit("page");
    await analytics.LastDispatch.WaitAsync(TestContext.Current.CancellationToken);
    analytics.RecordLandingPageClick("page", new("item"));
    await analytics.LastDispatch.WaitAsync(TestContext.Current.CancellationToken);
    analytics.RecordRssFeedView("feed");
    await analytics.LastDispatch.WaitAsync(TestContext.Current.CancellationToken);
    analytics.RecordRssFeedItemView("item");
    await analytics.LastDispatch.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(5, handler.Requests.Count);
    Assert.All(handler.Requests, request => Assert.Null(request.Proof));
  }

  private static ApiRequest Mutation() => new(HttpMethod.Patch, "/api/v1/items/item")
  {
    Query = new Dictionary<string, string> { ["query"] = "two words" },
  };

  private sealed record Ok(bool Success);
  private sealed record Captured(string Method, string Uri, string? Body, string? Proof, string? IdempotencyKey);

  private sealed class Handler : HttpMessageHandler
  {
    public string Outcome { get; set; } = "challenge";
    public List<Captured> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var proof = Header(request, "x-voucha-bot-challenge-token");
      Requests.Add(new(request.Method.Method, request.RequestUri!.AbsoluteUri,
          request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
          proof, Header(request, "Idempotency-Key")));
      if (Outcome == "network") throw new HttpRequestException("Ambiguous failure");
      if (Outcome == "success" || (proof is not null && Outcome != "repeat"))
        return new(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true}") };
      var body = ApiFixtureLoader.LoadResponse("native.bot-challenge.required");
      if (Outcome == "ordinary") body = "{\"code\":\"FORBIDDEN\"}";
      if (Outcome == "wrong-action") body = body.Replace("bot-challenge.mutation", "posts.create", StringComparison.Ordinal);
      if (Outcome == "bad-json") body = "invalid";
      return new(Outcome == "wrong-status" ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden)
      { Content = new StringContent(body) };
    }
    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? Assert.Single(values) : null;
  }

  private sealed class Session : ISessionStore
  {
    private EventHandler<SessionChangedEventArgs>? changed;
    public int Subscribers { get; private set; }
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { changed += value; Subscribers++; }
      remove { changed -= value; Subscribers--; }
    }
    public SessionSnapshot Current { get; private set; } = new(new User("account", "alice"));
    public void Set(string? account)
    {
      Current = account is null ? SessionSnapshot.Anonymous : new(new User(account, "alice"));
      changed?.Invoke(this, new(Current));
    }
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) { Set(null); return Task.CompletedTask; }
  }
}

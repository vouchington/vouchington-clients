using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class AuthCoverageTests
{
  [Fact]
  public async Task AuthenticationServiceCompletesEmailOtpMfaAndPasskeyFlows()
  {
    var handler = new QueueHandler(
        ("{}", HttpStatusCode.OK),
        ("""{"mfa_required":true,"login_attempt_id":"attempt-1"}""", HttpStatusCode.OK),
        ("{}", HttpStatusCode.OK),
        (IdentityJson("alice"), HttpStatusCode.OK),
        ("{}", HttpStatusCode.OK),
        ("""{"options":{"challenge":"Y2hhbGxlbmdl","rpId":"example.com","userVerification":"required","allowCredentials":[{"id":"Y3JlZGVudGlhbA"}]}}""", HttpStatusCode.OK),
        ("""{"mfa_required":false}""", HttpStatusCode.OK),
        (IdentityJson("alice"), HttpStatusCode.OK));
    var cookieContainer = new CookieContainer();
    var client = CreateClient(handler);
    var store = NewStore(client, cookieContainer);
    var passkeyProvider = new RecordingPasskeyProvider();
    var service = new AuthenticationService(client, store, passkeyProvider);

    await service.RequestEmailOtpAsync("a@example.com", "turnstile", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/auth/email-address/tokens", handler.Requests[0].PathAndQuery);

    var challenge = await service.VerifyEmailOtpAsync(
        "a@example.com",
        "123456",
        TestContext.Current.CancellationToken);
    Assert.Equal("attempt-1", challenge?.LoginAttemptId);

    await service.VerifyMfaTotpAsync("attempt-1", "654321", TestContext.Current.CancellationToken);
    Assert.True(store.Current.IsAuthenticated);

    await store.SignOutAsync(TestContext.Current.CancellationToken);
    Assert.False(store.Current.IsAuthenticated);

    var passkeyChallenge = await service.SignInWithPasskeyAsync(TestContext.Current.CancellationToken);
    Assert.Null(passkeyChallenge);
    Assert.Equal("example.com", passkeyProvider.LastOptions?.RpId);
    Assert.True(store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task AuthenticationServiceCompletesAppleSignInAndRequiresProvider()
  {
    var handler = new QueueHandler(
        ("""{"mfa_required":false}""", HttpStatusCode.OK),
        (IdentityJson("apple"), HttpStatusCode.OK));
    var cookieContainer = new CookieContainer();
    var client = CreateClient(handler);
    var store = NewStore(client, cookieContainer);
    var provider = new RecordingAppleSignInProvider(
        new AppleSignInCredential("token-1", "nonce-1", "Alice"));
    var service = new AuthenticationService(client, store, new RecordingPasskeyProvider(), provider);

    var challenge = await service.SignInWithAppleAsync(TestContext.Current.CancellationToken);

    Assert.Null(challenge);
    Assert.Equal("/api/v1/auth/oauth/apple/continue", handler.Requests[0].PathAndQuery);
    Assert.Equal(
        """{"token":"token-1","nonce":"nonce-1","userData":{"name":"Alice"}}""",
        handler.Requests[0].Body);
    Assert.True(store.Current.IsAuthenticated);
    Assert.Equal("token-1", provider.LastCredential?.Token);

    var missingProviderClient = CreateClient(new QueueHandler(
        ("""{"mfa_required":false}""", HttpStatusCode.OK)));
    var missingProviderService = new AuthenticationService(
        missingProviderClient,
        NewStore(missingProviderClient, new CookieContainer()),
        new RecordingPasskeyProvider());

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => missingProviderService.SignInWithAppleAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task AuthenticationServiceRejectsIncompleteAuthResponses()
  {
    var client = CreateClient(new QueueHandler(
        ("""{"mfa_required":true}""", HttpStatusCode.OK),
        ("""{"mfa_required":false}""", HttpStatusCode.OK),
        ("""{"error":"unauthorized"}""", HttpStatusCode.Unauthorized)));
    var store = NewStore(client, new CookieContainer());
    var service = new AuthenticationService(client, store, new RecordingPasskeyProvider());

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => service.VerifyEmailOtpAsync("a@example.com", "123456", TestContext.Current.CancellationToken));

    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => service.VerifyEmailOtpAsync("a@example.com", "123456", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task AuthenticationServiceRejectsMissingPasskeyOptionsAndMfaSession()
  {
    var missingOptionsClient = CreateClient(new QueueHandler(
        ("""{"options":null}""", HttpStatusCode.OK)));
    var missingOptionsService = new AuthenticationService(
        missingOptionsClient,
        NewStore(missingOptionsClient, new CookieContainer()),
        new RecordingPasskeyProvider());

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => missingOptionsService.SignInWithPasskeyAsync(TestContext.Current.CancellationToken));

    var mfaClient = CreateClient(new QueueHandler(
        ("{}", HttpStatusCode.OK),
        ("""{"error":"unauthorized"}""", HttpStatusCode.Unauthorized)));
    var mfaService = new AuthenticationService(
        mfaClient,
        NewStore(mfaClient, new CookieContainer()),
        new RecordingPasskeyProvider());

    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => mfaService.VerifyMfaTotpAsync("attempt-1", "123456", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task CookieSessionStoreHandlesUnauthorizedRefreshAndSeededCookies()
  {
    var cookieContainer = new CookieContainer();
    var store = NewStore(
        CreateClient(new QueueHandler(
            ("""{"error":"unauthorized"}""", HttpStatusCode.Unauthorized),
            (IdentityJson("dev"), HttpStatusCode.OK))),
        cookieContainer);
    var events = 0;
    store.SessionChanged += (_, _) => events++;

    await ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.False(store.Current.IsAuthenticated);

    AddSessionCookies(cookieContainer);
    await ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(store.Current.IsAuthenticated);
    var cookieHeader = cookieContainer.GetCookieHeader(new Uri("https://api.test"));
    Assert.Contains("dt=device", cookieHeader, StringComparison.Ordinal);
    Assert.Contains("st=session", cookieHeader, StringComparison.Ordinal);
    Assert.Equal(1, events);
  }

  [Fact]
  public async Task CookieSessionStoreClearsLocalSessionWhenLogoutTransportFails()
  {
    var cookieContainer = new CookieContainer();
    var persistence = new TestSessionCookiePersistence();
    var store = NewStore(
        CreateClient(new ThrowingHandler(new HttpRequestException("offline"))),
        cookieContainer,
        persistence);

    AddSessionCookies(cookieContainer);
    await ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(store.Current.IsAuthenticated);

    using var canceledSource = new CancellationTokenSource();
    await canceledSource.CancelAsync();

    await store.SignOutAsync(canceledSource.Token);

    Assert.False(store.Current.IsAuthenticated);
    Assert.Empty(cookieContainer.GetCookies(new Uri("https://api.test")).Cast<Cookie>());
    Assert.Null(persistence.Value);
    Assert.Equal(1, persistence.ClearCount);
  }

  [Fact]
  public async Task CookieSessionStoreForceRefreshIgnoresStaleStartupRefresh()
  {
    var handler = new DelayedIdentityHandler();
    var store = NewStore(CreateClient(handler), new CookieContainer());

    var staleRefresh = ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);
    await handler.FirstRequestStarted.WaitAsync(TestContext.Current.CancellationToken);

    await ((IForcedSessionRefreshStore)store)
        .RefreshAsync(force: true, TestContext.Current.CancellationToken);

    Assert.True(store.Current.IsAuthenticated);
    handler.CompleteFirstRequestAsUnauthorized();
    await staleRefresh;

    Assert.True(store.Current.IsAuthenticated);
    Assert.Equal(2, handler.RequestCount);
  }

  [Fact]
  public async Task CookieSessionStoreSignOutIgnoresStaleRefreshCompletion()
  {
    var handler = new DelayedIdentityHandler();
    var store = NewStore(CreateClient(handler), new CookieContainer());

    var staleRefresh = ((ISessionStore)store).RefreshAsync(TestContext.Current.CancellationToken);
    await handler.FirstRequestStarted.WaitAsync(TestContext.Current.CancellationToken);

    await store.SignOutAsync(TestContext.Current.CancellationToken);
    handler.CompleteFirstRequestAsIdentity();
    await staleRefresh;

    Assert.False(store.Current.IsAuthenticated);
  }

  [Fact]
  public async Task AuthViewModelUpdatesStatusForSuccessAndErrors()
  {
    var handler = new QueueHandler(
        ("{}", HttpStatusCode.OK),
        ("""{"mfa_required":true,"login_attempt_id":"attempt-1"}""", HttpStatusCode.OK),
        ("{}", HttpStatusCode.OK),
        (IdentityJson("alice"), HttpStatusCode.OK),
        ("""{"error":"logout failed"}""", HttpStatusCode.InternalServerError));
    var store = NewStore(CreateClient(handler), new CookieContainer());
    var viewModel = new AuthViewModel(
        new AuthenticationService(CreateClient(handler), store, new RecordingPasskeyProvider()),
        store)
    {
      Email = "a@example.com",
      TurnstileToken = "turnstile",
      Code = "123456",
      TotpCode = "654321",
    };

    viewModel.SetStatusMessage("Turnstile verification is not configured.");
    Assert.Equal("Turnstile verification is not configured.", viewModel.StatusMessage);

    await viewModel.RequestEmailOtpAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Email code requested.", viewModel.StatusMessage);

    await viewModel.VerifyEmailOtpAsync(TestContext.Current.CancellationToken);
    Assert.Equal("MFA code required.", viewModel.StatusMessage);

    await viewModel.VerifyTotpAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAuthenticated);
    Assert.Equal("Signed in.", viewModel.StatusMessage);
    Assert.Equal("Signed in as alice", viewModel.SessionLabel);

    await viewModel.SignOutAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Signed out.", viewModel.StatusMessage);

    await viewModel.VerifyTotpAsync(TestContext.Current.CancellationToken);
    Assert.Equal("No MFA challenge is active.", viewModel.StatusMessage);
  }

  [Fact]
  public async Task AuthViewModelTracksEmailOtpResendAndDeepLinkPrefillState()
  {
    var handler = new QueueHandler(
        ("{}", HttpStatusCode.OK),
        ("{}", HttpStatusCode.OK),
        ("""{"mfa_required":false}""", HttpStatusCode.OK),
        (IdentityJson("alice"), HttpStatusCode.OK),
        ("""{"mfa_required":false}""", HttpStatusCode.OK),
        (IdentityJson("alice"), HttpStatusCode.OK),
        ("""{"mfa_required":false}""", HttpStatusCode.OK),
        (IdentityJson("alice"), HttpStatusCode.OK));
    var store = NewStore(CreateClient(handler), new CookieContainer());
    var viewModel = new AuthViewModel(
        new AuthenticationService(CreateClient(handler), store, new RecordingPasskeyProvider()),
        store)
    {
      Email = "a@example.com",
      TurnstileToken = "turnstile",
      Code = "12345678",
    };

    Assert.False(viewModel.HasRequestedEmailOtp);
    Assert.False(viewModel.CanResendEmailOtp);
    viewModel.ApplyEmailOtpPrefill("a@example.com", "12345678");

    Assert.Equal("a@example.com", viewModel.Email);
    Assert.Equal("12345678", viewModel.Code);

    await viewModel.RequestEmailOtpAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasRequestedEmailOtp);
    Assert.True(viewModel.CanResendEmailOtp);
    Assert.Equal("Email code requested.", viewModel.StatusMessage);

    await viewModel.RequestEmailOtpAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Email code resent.", viewModel.StatusMessage);

    await viewModel.VerifyEmailOtpAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsAuthenticated);
    Assert.Equal("Signed in.", viewModel.StatusMessage);
  }

  [Fact]
  public void SessionAndPasskeyModelsExposeExpectedDefaults()
  {
    var identity = new User("user-1", "alice", Roles: ["user"], EmailAddress: "a@example.com", MembershipPlan: "free");
    var session = new SessionSnapshot(identity);
    var args = new SessionChangedEventArgs(session);
    var response = new PasskeyAssertionResponse(
        "id",
        "raw",
        new PasskeyAssertionAuthenticatorResponse("auth", "client", "sig", "user"));

    Assert.True(session.IsAuthenticated);
    Assert.False(SessionSnapshot.Anonymous.IsAuthenticated);
    Assert.Same(session, args.Snapshot);
    Assert.Equal("public-key", response.Type);
    Assert.Empty(response.ClientExtensionResults);
  }

  [Fact]
  public void CanManageTopicsHandlesNullRoles()
  {
    var session = new SessionSnapshot(new User("user-1", "alice", Roles: null!, EmailAddress: "a@example.com", MembershipPlan: "free"));

    Assert.False(session.CanManageTopics());
  }

  private static VouchaApiClient CreateClient(HttpMessageHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private static CookieSessionStore NewStore(
      VouchaApiClient client,
      CookieContainer cookieContainer,
      TestSessionCookiePersistence? persistence = null) =>
      new(client, new SessionCookieJar(
          cookieContainer,
          new Uri("https://api.test"),
          persistence ?? new TestSessionCookiePersistence()));

  private static void AddSessionCookies(CookieContainer cookieContainer)
  {
    cookieContainer.Add(new Uri("https://api.test"), new Cookie("dt", "device", "/", "api.test")
    {
      HttpOnly = true,
      Secure = true,
    });
    cookieContainer.Add(new Uri("https://api.test"), new Cookie("st", "session", "/", "api.test")
    {
      HttpOnly = true,
      Secure = true,
    });
  }

  private static string IdentityJson(string username) =>
      $$"""
        {
          "identity": {
            "id": "user-1",
            "username": "{{username}}",
            "roles": ["user"],
            "email_address": "{{username}}@example.com",
            "membership_plan": "free"
          }
        }
        """;

  private sealed class RecordingPasskeyProvider : IPasskeyAssertionProvider
  {
    public PasskeyAuthenticationOptions? LastOptions { get; private set; }

    public Task<PasskeyAssertionResponse> GetAssertionAsync(
        PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default)
    {
      LastOptions = options;
      return Task.FromResult(new PasskeyAssertionResponse(
          "credential",
          "credential",
          new PasskeyAssertionAuthenticatorResponse("auth", "client", "signature", "user")));
    }
  }

  private sealed class RecordingAppleSignInProvider : IAppleSignInProvider
  {
    public RecordingAppleSignInProvider(AppleSignInCredential credential) =>
        LastCredential = credential;

    public AppleSignInCredential LastCredential { get; }

    public Task<AppleSignInCredential> GetCredentialAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(LastCredential);
  }

  private sealed class QueueHandler : HttpMessageHandler
  {
    private readonly Queue<(string Body, HttpStatusCode StatusCode)> responses;

    public QueueHandler(params (string Body, HttpStatusCode StatusCode)[] responses) =>
        this.responses = new Queue<(string Body, HttpStatusCode StatusCode)>(responses);

    public List<(HttpMethod Method, string? PathAndQuery, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var requestBody = request.Content is null
          ? null
          : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      Requests.Add((request.Method, request.RequestUri?.PathAndQuery, requestBody));
      var (body, statusCode) = responses.Dequeue();
      return new HttpResponseMessage(statusCode)
      {
        Content = new StringContent(body),
        RequestMessage = request,
      };
    }
  }

  private sealed class ThrowingHandler : HttpMessageHandler
  {
    private readonly Exception exception;
    private bool responded;

    public ThrowingHandler(Exception exception) =>
        this.exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      if (!responded)
      {
        responded = true;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(IdentityJson("dev")),
        });
      }

      return Task.FromException<HttpResponseMessage>(exception);
    }
  }

  private sealed class DelayedIdentityHandler : HttpMessageHandler
  {
    private readonly TaskCompletionSource<HttpResponseMessage> firstResponse = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource firstRequestStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int requestCount;

    public Task FirstRequestStarted => firstRequestStarted.Task;

    public int RequestCount => requestCount;

    public void CompleteFirstRequestAsUnauthorized() =>
        firstResponse.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

    public void CompleteFirstRequestAsIdentity() =>
        firstResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(IdentityJson("stale")),
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      if (Interlocked.Increment(ref requestCount) == 1)
      {
        firstRequestStarted.SetResult();
        return firstResponse.Task;
      }

      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(IdentityJson("fresh")),
        RequestMessage = request,
      });
    }
  }
}

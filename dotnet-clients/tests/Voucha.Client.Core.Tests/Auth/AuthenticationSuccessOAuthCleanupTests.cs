using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class AuthenticationSuccessOAuthCleanupTests
{
  [Fact]
  public async Task EverySuccessfulAuthenticationDiscardsSupersededNativeOAuthState()
  {
    var handler = new QueueHandler(
        """{"mfa_required":true,"login_attempt_id":"attempt-1"}""",
        "{}",
        """{"mfa_required":false}""",
        """{"options":{"challenge":"Y2hhbGxlbmdl","rpId":"example.com","userVerification":"required","allowCredentials":[]}}""",
        """{"mfa_required":false}""",
        """{"mfa_required":false}""");
    var client = new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    });
    var cleanup = new RecordingAuthorizationDiscarder();
    var service = new AuthenticationService(
        client,
        new AuthenticatedSessionStore(),
        new PasskeyProvider(),
        new AppleProvider(),
        cleanup);

    var challenge = await service.VerifyEmailOtpAsync(
        "a@example.com",
        "123456",
        TestContext.Current.CancellationToken);
    Assert.NotNull(challenge);
    Assert.Equal(0, cleanup.DiscardCount);

    await service.VerifyMfaTotpAsync(
        challenge.LoginAttemptId,
        "654321",
        TestContext.Current.CancellationToken);
    await service.VerifyEmailOtpAsync(
        "a@example.com",
        "123456",
        TestContext.Current.CancellationToken);
    await service.SignInWithPasskeyAsync(TestContext.Current.CancellationToken);
    await service.SignInWithAppleAsync(TestContext.Current.CancellationToken);

    Assert.Equal(4, cleanup.DiscardCount);
  }

  [Fact]
  public async Task MissingAuthenticatedSessionPreservesNativeOAuthState()
  {
    var client = new VouchaApiClient(new HttpClient(new QueueHandler(
        """{"mfa_required":false}"""))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    var cleanup = new RecordingAuthorizationDiscarder();
    var service = new AuthenticationService(
        client,
        new AnonymousSessionStore(),
        new PasskeyProvider(),
        null,
        cleanup);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        service.VerifyEmailOtpAsync(
            "a@example.com",
            "123456",
            TestContext.Current.CancellationToken));

    Assert.Equal(0, cleanup.DiscardCount);
  }

  private sealed class RecordingAuthorizationDiscarder :
      ISupersededNativeOAuthAuthorizationDiscarder
  {
    public int DiscardCount { get; private set; }

    public Task DiscardAfterSuccessfulAuthenticationAsync()
    {
      DiscardCount++;
      return Task.CompletedTask;
    }
  }

  private sealed class AuthenticatedSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
      Current = new SessionSnapshot(new User("user-1", "alice"));
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
      return Task.CompletedTask;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current => SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }

  private sealed class PasskeyProvider : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(
        PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PasskeyAssertionResponse(
            "credential",
            "credential",
            new PasskeyAssertionAuthenticatorResponse("auth", "client", "signature", "user")));
  }

  private sealed class AppleProvider : IAppleSignInProvider
  {
    public Task<AppleSignInCredential> GetCredentialAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppleSignInCredential("token", "nonce", "Alice"));
  }

  private sealed class QueueHandler(params string[] responses) : HttpMessageHandler
  {
    private readonly Queue<string> remaining = new(responses);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(remaining.Dequeue()),
          RequestMessage = request,
        });
  }
}

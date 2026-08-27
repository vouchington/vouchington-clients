using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class AuthViewModelMfaCancellationTests
{
  [Fact]
  public async Task ClearingMfaChallengePreventsRetainedTotpSubmission()
  {
    var handler = new RecordingHandler();
    var session = new AnonymousSessionStore();
    var viewModel = new AuthViewModel(
        new AuthenticationService(
            new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
            session,
            new UnusedPasskeyProvider()),
        session)
    {
      TotpCode = "123456",
    };
    viewModel.AcceptMfaChallenge("oauth-mfa-attempt");

    viewModel.ClearMfaChallenge();
    await viewModel.VerifyTotpAsync(TestContext.Current.CancellationToken);

    Assert.Equal("", viewModel.TotpCode);
    Assert.Equal("No MFA challenge is active.", viewModel.StatusMessage);
    Assert.Equal(0, handler.RequestCount);
  }

  private sealed class RecordingHandler : HttpMessageHandler
  {
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      RequestCount++;
      throw new InvalidOperationException("Cancelled MFA must not reach the API.");
    }
  }

  private sealed class UnusedPasskeyProvider : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(
        PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

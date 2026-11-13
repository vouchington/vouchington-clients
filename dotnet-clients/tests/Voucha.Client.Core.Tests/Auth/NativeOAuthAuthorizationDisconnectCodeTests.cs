using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class NativeOAuthAuthorizationDisconnectCodeTests
{
  [Theory]
  [InlineData("LAST_SIGN_IN_METHOD_REMOVAL_FORBIDDEN", false)]
  [InlineData("ADMINISTRATOR_MFA_REQUIRED", false)]
  [InlineData("UNKNOWN_CODE", true)]
  public async Task DisconnectFailureUsesLocalizedCopyOnlyForKnownCodes(string code, bool generic)
  {
    using var coordinator = new NativeOAuthAuthorizationCoordinator(
        new VouchaApiClient(new HttpClient(new Handler(code)) { BaseAddress = new Uri("https://api.test") }),
        new NoPersistence(),
        new NoBrowser(),
        new NoSession());

    await coordinator.DisconnectAsync(OAuthBrokerProvider.Github, TestContext.Current.CancellationToken);

    Assert.Equal(NativeOAuthAuthorizationState.Failed, coordinator.State);
    Assert.Equal(generic, coordinator.ErrorMessage!.Contains("HTTP 400", StringComparison.Ordinal));
    Assert.False(string.IsNullOrWhiteSpace(coordinator.ErrorMessage));
  }

  private sealed class Handler(string code) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
          Content = new StringContent($$"""{"code":"{{code}}"}""", Encoding.UTF8, "application/json"),
        });
  }

  private sealed class NoBrowser : INativeExternalBrowser
  {
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
  }

  private sealed class NoSession : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current { get; } = new(new User("u", "n"));

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class NoPersistence : INativeOAuthAuthorizationPersistence
  {
    public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task WritePendingAsync(PendingNativeOAuthAuthorization pending, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task<bool> ClaimCallbackAsync(string flowId, string completionToken, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task<bool> CompleteAsync(string flowId, NativeOAuthAuthorizationResult result, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();

    public Task ClearPendingAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
  }
}

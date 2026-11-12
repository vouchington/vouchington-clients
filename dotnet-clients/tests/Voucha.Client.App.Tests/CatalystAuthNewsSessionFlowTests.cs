#if DEBUG
using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.App.Tests;

// Production auth/session composition over the deterministic backend; no GUI execution.
public sealed class CatalystAuthNewsSessionFlowTests
{
  [Fact]
  public async Task EmailControlsPipelineCreatesSessionOnlyAfterValidCode()
  {
    var origin = new Uri("https://ui-testing.voucha.invalid");
    var persistence = new CatalystAuthNewsSessionPersistence();
    using var jar = new SessionCookieJar(new CookieContainer(), origin, persistence);
    using var http = new HttpClient(new ClientMetadataHandler(new ClientMetadata(ClientPlatform.MacOS, "test"),
        origin, jar, new SessionCookiePersistenceHandler(jar, new CatalystAuthNewsHandler()))) { BaseAddress = origin };
    var api = new VouchaApiClient(http);
    ISessionStore session = new CookieSessionStore(api, jar);
    await session.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(jar.HasDeviceCookie);
    Assert.False(session.Current.IsAuthenticated);
    using var model = new AuthViewModel(new AuthenticationService(api, session, new UnsupportedPasskey()), session);
    Assert.False(model.IsAuthenticated);
    var turnstile = new CatalystAuthNewsTurnstile(true);
    model.Email = CatalystAuthNewsLaunch.Email;
    model.TurnstileToken = await turnstile.GetTokenAsync("auth.email-address-tokens", TestContext.Current.CancellationToken);
    await model.RequestEmailOtpAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasRequestedEmailOtp);
    Assert.False(model.IsAuthenticated);
    model.Code = "WRONGCODE1";
    await model.VerifyEmailOtpAsync(TestContext.Current.CancellationToken);
    Assert.False(model.IsAuthenticated);
    model.Code = CatalystAuthNewsLaunch.Code;
    await model.VerifyEmailOtpAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsAuthenticated);
    Assert.Equal("alice", session.Current.Identity!.Username);
    Assert.Contains("alice", model.SessionLabel, StringComparison.Ordinal);
    Assert.Contains("catalyst-flow-session", await persistence.ReadAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
  }

  [Fact]
  public async Task TurnstileSeamRefusesNonFlowAndWrongAction()
  {
    await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalystAuthNewsTurnstile(false)
        .GetTokenAsync("auth.email-address-tokens", TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalystAuthNewsTurnstile(true)
        .GetTokenAsync("other-action", TestContext.Current.CancellationToken));
  }

  private sealed class UnsupportedPasskey : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
#endif

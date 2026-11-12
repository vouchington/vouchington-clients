#if DEBUG
using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
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
    var bootstrapPersistence = await persistence.ReadAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(bootstrapPersistence);
    using (var restoredBootstrap = new SessionCookieJar(new CookieContainer(), origin, persistence))
    {
      await restoredBootstrap.RestoreAsync(TestContext.Current.CancellationToken);
      Assert.Equal("catalyst-bootstrap-device", restoredBootstrap.CookieContainer.GetCookies(origin)["dt"]!.Value);
      Assert.Equal("catalyst-bootstrap-session", restoredBootstrap.CookieContainer.GetCookies(origin)["st"]!.Value);
    }
    using var locale = new UiLocaleController(CatalystAuthNewsLaunch.DeviceLanguages(new StubDeviceLanguageProvider("es"), true));
    locale.AttachSession(session);
    using var model = new AuthViewModel(new AuthenticationService(api, session, new UnsupportedPasskey()), session,
        locale, new UiLocalization(locale));
    Assert.Equal("en", locale.EffectiveLocale);
    Assert.Equal("Signed out", model.SessionLabel);
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
    Assert.Equal("en", locale.EffectiveLocale);
    Assert.Equal("Signed in as alice", model.SessionLabel);
    var loginPersistence = await persistence.ReadAsync(TestContext.Current.CancellationToken);
    Assert.NotEqual(bootstrapPersistence, loginPersistence);
    using var restoredLogin = new SessionCookieJar(new CookieContainer(), origin, persistence);
    await restoredLogin.RestoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("catalyst-flow-device", restoredLogin.CookieContainer.GetCookies(origin)["dt"]!.Value);
    Assert.Equal("catalyst-flow-session", restoredLogin.CookieContainer.GetCookies(origin)["st"]!.Value);
  }

  [Fact]
  public async Task TurnstileSeamRefusesNonFlowAndWrongAction()
  {
    await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalystAuthNewsTurnstile(false)
        .GetTokenAsync("auth.email-address-tokens", TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalystAuthNewsTurnstile(true)
        .GetTokenAsync("other-action", TestContext.Current.CancellationToken));
  }

  [Fact]
  public void NormalLaunchKeepsNonEnglishDeviceLocale()
  {
    using var locale = new UiLocaleController(CatalystAuthNewsLaunch.DeviceLanguages(new StubDeviceLanguageProvider("es"), false));
    Assert.Equal("es", locale.EffectiveLocale);
  }

  [Theory]
  [InlineData("es")]
  [InlineData("fr")]
  public void FlowRejectsIdentityFixtureThatWouldOverridePinnedEnglish(string savedLocale)
  {
    var identity = "{\"identity\":{\"ui_locale\":\"" + savedLocale + "\"}}";
    Assert.Throws<InvalidOperationException>(() => CatalystAuthNewsLaunch.RequireEnglishIdentity(identity));
  }

  private sealed class UnsupportedPasskey : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
  }
}
#endif

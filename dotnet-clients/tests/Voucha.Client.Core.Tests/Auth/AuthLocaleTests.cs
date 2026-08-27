using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class AuthLocaleTests
{
  [Fact]
  public async Task RequestEmailOtpAsyncForwardsUiLocale()
  {
    var handler = new RecordingHandler("{}", HttpStatusCode.OK);
    var service = new AuthenticationService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
        new DummySessionStore(),
        new DummyPasskeyProvider());

    await service.RequestEmailOtpAsync(
        "a@example.com",
        "turnstile",
        "fr",
        TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/auth/email-address/tokens", handler.PathAndQuery);
    Assert.Contains("\"ui_locale\":\"fr\"", handler.RequestBody, StringComparison.Ordinal);
  }

  [Fact]
  public async Task AuthViewModelUsesTheCurrentRuntimeLocaleForEmailOtp()
  {
    var handler = new RecordingHandler("{}", HttpStatusCode.OK);
    var sessionStore = new DummySessionStore();
    var localeController = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var viewModel = new AuthViewModel(
        new AuthenticationService(
            new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
            sessionStore,
            new DummyPasskeyProvider()),
        sessionStore,
        localeController)
    {
      Email = "a@example.com",
      TurnstileToken = "turnstile",
    };
    localeController.ApplySavedLocale("pt-BR");

    await viewModel.RequestEmailOtpAsync(TestContext.Current.CancellationToken);

    Assert.Contains("\"ui_locale\":\"pt\"", handler.RequestBody, StringComparison.Ordinal);
  }

  private sealed class DummySessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add { }
      remove { }
    }

    public SessionSnapshot Current => SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class DummyPasskeyProvider : IPasskeyAssertionProvider
  {
    public Task<PasskeyAssertionResponse> GetAssertionAsync(
        PasskeyAuthenticationOptions options,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}

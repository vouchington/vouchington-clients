#if DEBUG
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Auth;

public sealed partial class AuthViewModel
{
  private string deviceToken = "";
  private string sessionToken = "";

  public string DeviceToken
  {
    get => deviceToken;
    set => SetProperty(ref deviceToken, value);
  }

  public string SessionToken
  {
    get => sessionToken;
    set => SetProperty(ref sessionToken, value);
  }

  public async Task InjectDevelopmentCookiesAsync(CancellationToken cancellationToken = default)
  {
    if (sessionStore is not IDevelopmentSessionStore developmentSessionStore)
    {
      throw new InvalidOperationException(
          localization.Localize(
              UiMessageKey.NativeDotnetAuthDevelopmentCookieLoadingUnavailable));
    }

    await RunAsync(async () =>
    {
      await developmentSessionStore
          .InjectDevelopmentCookiesAsync(DeviceToken, SessionToken, cancellationToken)
          .ConfigureAwait(true);
      SetLocalizedStatusMessage(
          sessionStore.Current.IsAuthenticated
              ? UiMessageKey.NativeDotnetAuthDevelopmentSessionLoaded
              : UiMessageKey.NativeDotnetAuthDevelopmentCookiesStoredWithoutIdentity);
    }).ConfigureAwait(true);
  }
}
#endif

using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed partial class CookieSessionStore
{
  public async Task SignOutAsync(CancellationToken cancellationToken = default)
  {
    lock (syncLock) { refreshGeneration++; signingOut = true; }
    cookieJar.Invalidate();
    SetCurrent(SessionSnapshot.Anonymous);
    try
    {
      var cookies = await cookieJar.ClearForSignOutAsync().ConfigureAwait(false);
      await client
          .SendAsync(VouchaApiEndpoints.Logout() with
          {
            Headers = new Dictionary<string, string> { ["Cookie"] = cookies },
          }, cancellationToken)
          .ConfigureAwait(false);
    }
    catch (VouchaApiException)
    {
      // Clear local session state even if the server session is already gone.
    }
    catch (HttpRequestException)
    {
      // Clear local session state even when the network is unavailable.
    }
    catch (TaskCanceledException)
    {
      // Clear local session state when the logout request times out.
    }
    finally
    {
      lock (syncLock) signingOut = false;
    }
  }

#if DEBUG
  public async Task InjectDevelopmentCookiesAsync(
      string deviceToken,
      string sessionToken,
      CancellationToken cancellationToken = default)
  {
    await cookieJar
        .StoreDevelopmentCookiesAsync(deviceToken, sessionToken, cancellationToken)
        .ConfigureAwait(false);
    await RefreshCoreAsync(
        force: true,
        reportTransientFailure: false,
        cancellationToken).ConfigureAwait(false);
  }
#endif

}

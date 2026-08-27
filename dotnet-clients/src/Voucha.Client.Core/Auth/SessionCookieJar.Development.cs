#if DEBUG
namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar
{
  public async Task StoreDevelopmentCookiesAsync(
      string deviceToken,
      string sessionToken,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(deviceToken);
    ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      SetCookie("dt", deviceToken);
      SetCookie("st", sessionToken);
      await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }
}
#endif

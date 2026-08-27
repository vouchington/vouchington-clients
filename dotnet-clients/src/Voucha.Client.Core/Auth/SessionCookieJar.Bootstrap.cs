namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar
{
  public bool HasDeviceCookie => ReadCookie("dt") is not null;

  public async Task StoreSessionTokensAsync(
      string deviceToken,
      string sessionToken,
      TimeSpan deviceLifetime,
      TimeSpan sessionLifetime,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(deviceToken);
    ArgumentException.ThrowIfNullOrEmpty(sessionToken);
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      var now = DateTimeOffset.UtcNow;
      SetCookie("dt", deviceToken, now.Add(deviceLifetime));
      SetCookie("st", sessionToken, now.Add(sessionLifetime));
      await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }
}

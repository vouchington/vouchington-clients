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
    var generation = BeginSessionTransition();
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (generation != Generation) throw new OperationCanceledException("Credential replacement was superseded.");
      var before = (ReadCookie("dt")?.Value, ReadCookie("st")?.Value);
      var now = DateTimeOffset.UtcNow;
      var deviceExpiry = now.Add(deviceLifetime);
      var sessionExpiry = now.Add(sessionLifetime);
      SetCookie("dt", deviceToken, deviceExpiry);
      SetCookie("st", sessionToken, sessionExpiry);
      authenticatedSession = null;
      if (before != (ReadCookie("dt")?.Value, ReadCookie("st")?.Value))
        CommitCredentialChange(preserveAuthentication: false);
      await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }
}

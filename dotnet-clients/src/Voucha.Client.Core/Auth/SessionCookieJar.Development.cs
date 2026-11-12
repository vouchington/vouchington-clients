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

    var generation = BeginSessionTransition();
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (generation != Generation) throw new OperationCanceledException("Credential replacement was superseded.");
      var before = (ReadCookie("dt")?.Value, ReadCookie("st")?.Value);
      SetCookie("dt", deviceToken);
      SetCookie("st", sessionToken);
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
#endif

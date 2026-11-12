namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar
{
  public Task WaitForRenewalAsync(CancellationToken cancellationToken = default)
  {
    lock (renewalLock)
      return (renewalTaskEpoch == RenewalEpoch && renewalTask is { IsCompleted: false } pending ? pending : Task.CompletedTask)
          .WaitAsync(cancellationToken);
  }

  internal async Task<(long Generation, long Revision, string Header)> PrepareRequestAsync(
      Uri uri, bool sessionRequest, CancellationToken cancellationToken, long? authenticationIntent = null)
  {
    await RestoreAsync(cancellationToken).ConfigureAwait(false);
    while (true)
    {
      Task? pending;
      await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        lock (renewalLock)
        {
          pending = !sessionRequest && authenticationIntent is null && renewalTaskEpoch == RenewalEpoch && renewalTask is { IsCompleted: false } ? renewalTask : null;
          if (pending is null)
          {
            if (authenticationIntent is { } intent && !IsAuthenticationCurrent(intent))
              throw new OperationCanceledException("Authentication request was superseded.");
            return (Generation, CredentialRevision, GetCookieHeader(uri));
          }
        }
      }
      finally
      {
        sync.Release();
      }
      await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  public async Task ApplyResponseCookiesAsync(
      Uri? requestUri, IEnumerable<string> headers, long epoch, long revision, bool authenticationCommit,
      CancellationToken cancellationToken, long? authenticationIntent = null)
  {
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      lock (authenticationLock)
      {
        if (authenticationIntent is { } intent && !IsAuthenticationCurrent(intent)) return;
        if ((!authenticationCommit || authenticationIntent is null) && epoch != Generation) return;
        if (!authenticationCommit && revision != CredentialRevision) return;
        var before = (ReadCookie("dt")?.Value, ReadCookie("st")?.Value);
        StoreSetCookieHeaders(requestUri, headers);
        if (before != (ReadCookie("dt")?.Value, ReadCookie("st")?.Value))
        {
          authenticatedSession = null;
          CommitCredentialChange(preserveAuthentication: authenticationCommit && authenticationIntent is not null);
        }
      }
      await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }

  internal bool IsFirstParty(Uri uri) => IsSameOrigin(uri);

  internal async Task<string?> ClearForSignOutAsync(long epoch)
  {
    await RestoreAsync().ConfigureAwait(false);
    await sync.WaitAsync().ConfigureAwait(false);
    try
    {
      if (epoch != Generation) return null;
      var header = cookieContainer.GetCookieHeader(apiBaseUrl);
      await ClearUnlockedAsync(preserveAuthentication: false, cancellationToken: CancellationToken.None).ConfigureAwait(false);
      return header;
    }
    finally
    {
      sync.Release();
    }
  }

  public string GetCookieHeader(Uri uri)
  {
    ArgumentNullException.ThrowIfNull(uri);
    return IsSameOrigin(uri) ? cookieContainer.GetCookieHeader(uri) : "";
  }
}

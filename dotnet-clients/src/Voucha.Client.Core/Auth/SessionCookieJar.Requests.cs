namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar
{
  public Task WaitForRenewalAsync(CancellationToken cancellationToken = default)
  {
    lock (renewalLock)
      return (renewalGeneration == Generation && renewalTask is { IsCompleted: false } pending ? pending : Task.CompletedTask)
          .WaitAsync(cancellationToken);
  }

  internal async Task<(long Generation, long Revision, string Header)> PrepareRequestAsync(
      Uri uri, bool sessionRequest, CancellationToken cancellationToken)
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
          pending = !sessionRequest && renewalGeneration == Generation && renewalTask is { IsCompleted: false } ? renewalTask : null;
          if (pending is null) return (Generation, CredentialRevision, GetCookieHeader(uri));
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
      Uri? requestUri, IEnumerable<string> headers, long epoch, long revision, CancellationToken cancellationToken)
  {
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (epoch != Generation || revision != CredentialRevision) return;
      var before = (ReadCookie("dt"), ReadCookie("st"));
      StoreSetCookieHeaders(requestUri, headers);
      if (before != (ReadCookie("dt"), ReadCookie("st")))
      {
        Interlocked.Increment(ref credentialRevision);
        authenticatedSession = null;
        if (requestUri?.AbsolutePath.StartsWith("/api/v1/auth/", StringComparison.Ordinal) == true)
          Invalidate();
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
      await ClearUnlockedAsync(CancellationToken.None).ConfigureAwait(false);
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

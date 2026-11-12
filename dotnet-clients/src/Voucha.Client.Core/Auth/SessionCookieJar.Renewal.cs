using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar
{
  private readonly object renewalLock = new();
  private Task? renewalTask;
  private long renewalTaskEpoch;
  // Authentication dispatch fences older responses without abandoning consumed renewal credentials.
  private long renewalEpoch;
  private long generation;
  private readonly object authenticationLock = new();
  private long authenticationIntent;
  private long credentialRevision;

  public bool CanRecoverAuthentication => authenticatedSession != false && HasDeviceCookie;

  public long CredentialRevision => Interlocked.Read(ref credentialRevision);

  public long Generation => Interlocked.Read(ref generation);

  private long RenewalEpoch => Interlocked.Read(ref renewalEpoch);

  public event EventHandler? SessionInvalidated;

  private long AdvanceGeneration() => Interlocked.Increment(ref generation);

  internal long BeginAuthentication()
  {
    lock (authenticationLock)
    {
      var intent = Interlocked.Increment(ref authenticationIntent);
      AdvanceGeneration();
      return intent;
    }
  }

  private bool IsAuthenticationCurrent(long intent) => intent == Interlocked.Read(ref authenticationIntent);

  public long Invalidate()
  {
    lock (authenticationLock)
    {
      Interlocked.Increment(ref authenticationIntent);
      return InvalidateRenewal();
    }
  }

  private long InvalidateRenewal()
  {
    Interlocked.Increment(ref renewalEpoch);
    var epoch = AdvanceGeneration();
    SessionInvalidated?.Invoke(this, EventArgs.Empty);
    return epoch;
  }

  public Task RenewAsync(
      Func<string?, string?, CancellationToken, Task<NativeSessionRefreshResponse>> renew,
      bool bootstrap = false,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(renew);
    Task shared;
    lock (renewalLock)
    {
      var epoch = RenewalEpoch;
      if (renewalTask is null || renewalTask.IsCompleted || renewalTaskEpoch != epoch)
      {
        renewalTaskEpoch = epoch;
        renewalTask = RenewInternalAsync(renew, bootstrap, epoch);
      }
      shared = renewalTask;
    }
    return shared.WaitAsync(cancellationToken);
  }

  private async Task RenewInternalAsync(
      Func<string?, string?, CancellationToken, Task<NativeSessionRefreshResponse>> renew,
      bool bootstrap,
      long epoch)
  {
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), renewalTimeProvider);
    var cancellationToken = deadline.Token;
    await RestoreAsync(cancellationToken).ConfigureAwait(false);
    var (deviceToken, sessionToken, revision) = await ReadRenewalTokensAsync(epoch, cancellationToken).ConfigureAwait(false);
    if (!bootstrap && deviceToken is null)
      throw new UnauthorizedAccessException("Session tokens are unavailable.");
    try
    {
      var result = await renew(deviceToken, sessionToken, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
      var replacement = result.Session;
      if (replacement is null || string.IsNullOrWhiteSpace(replacement.Dt) ||
          string.IsNullOrWhiteSpace(replacement.St) || replacement.Dte <= 0 || replacement.Ste <= 0 ||
          (!bootstrap && (string.IsNullOrEmpty(replacement.Uid) || replacement.Session?.Uid != replacement.Uid)))
        throw new UnauthorizedAccessException("Session renewal did not return an authenticated session.");
      await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        if (epoch != RenewalEpoch) throw new UnauthorizedAccessException("Session changed during renewal.");
        if (revision != CredentialRevision) return;
        var now = DateTimeOffset.UtcNow;
        SetCookie("dt", replacement.Dt, now.AddSeconds(replacement.Dte), replacement.Secure);
        SetCookie("st", replacement.St, now.AddSeconds(replacement.Ste), replacement.Secure);
        authenticatedSession = !string.IsNullOrEmpty(replacement.Uid);
        try
        {
          await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
          Interlocked.Increment(ref credentialRevision);
        }
        catch
        {
          InvalidateRenewal();
          await ClearUnlockedAsync(CancellationToken.None).ConfigureAwait(false);
          throw;
        }
      }
      finally
      {
        sync.Release();
      }
    }
    catch (Exception ex) when (IsUnusableRenewal(ex))
    {
      await ClearRenewalIfCurrentAsync(epoch, revision).ConfigureAwait(false);
      throw;
    }
  }

  private static bool IsUnusableRenewal(Exception exception) =>
      exception is UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or VouchaApiDecodeException ||
      exception is VouchaApiException api && api.StatusCode == System.Net.HttpStatusCode.Unauthorized;

  private async Task<(string? DeviceToken, string? SessionToken, long Revision)> ReadRenewalTokensAsync(long epoch, CancellationToken cancellationToken)
  {
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (epoch != RenewalEpoch) throw new UnauthorizedAccessException("Session changed before renewal.");
      return (ReadCookie("dt")?.Value, ReadCookie("st")?.Value, CredentialRevision);
    }
    finally
    {
      sync.Release();
    }
  }

  private async Task ClearRenewalIfCurrentAsync(long epoch, long revision)
  {
    await sync.WaitAsync().ConfigureAwait(false);
    try
    {
      if (epoch != RenewalEpoch || revision != CredentialRevision) return;
      InvalidateRenewal();
      await ClearUnlockedAsync(CancellationToken.None).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }

  internal async Task ClearIfCurrentAsync(long epoch, long? revision = null)
  {
    await sync.WaitAsync().ConfigureAwait(false);
    try
    {
      if (epoch != Generation || revision is not null && revision != CredentialRevision) return;
      Invalidate();
      await ClearUnlockedAsync(CancellationToken.None).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }

}

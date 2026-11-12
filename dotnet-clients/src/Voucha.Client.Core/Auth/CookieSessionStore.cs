using System.Net;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

#if DEBUG
public sealed partial class CookieSessionStore :
    ISessionStore,
    IForcedSessionRefreshStore,
    INativeLoginCredentialStore,
    IConfirmingSessionRefreshStore,
    ICommittedOAuthDisconnectSessionStore,
    IDevelopmentSessionStore
#else
public sealed partial class CookieSessionStore :
    ISessionStore,
    IForcedSessionRefreshStore,
    INativeLoginCredentialStore,
    IConfirmingSessionRefreshStore,
    ICommittedOAuthDisconnectSessionStore
#endif
{
  private readonly VouchaApiClient client;
  private readonly SessionCookieJar cookieJar;
  private readonly object syncLock = new();
  private SessionSnapshot current = SessionSnapshot.Anonymous;
  private Task? refreshTask;
  private long refreshGeneration;
  private long refreshCookieGeneration;
  private long identityRequestGeneration;
  private bool signingOut;
  private long? signOutGeneration;

  public CookieSessionStore(
      VouchaApiClient client,
      SessionCookieJar cookieJar)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.cookieJar = cookieJar ?? throw new ArgumentNullException(nameof(cookieJar));
    cookieJar.SessionInvalidated += OnSessionInvalidated;
  }

  private void OnSessionInvalidated(object? sender, EventArgs args)
  {
    lock (syncLock) refreshGeneration++;
    SetCurrent(SessionSnapshot.Anonymous);
  }

  public event EventHandler<SessionChangedEventArgs>? SessionChanged;

  public SessionSnapshot Current
  {
    get
    {
      lock (syncLock)
      {
        return current;
      }
    }
  }

  Task ISessionStore.RefreshAsync(CancellationToken cancellationToken) =>
      RefreshCoreAsync(force: false, reportTransientFailure: false, cancellationToken);

  Task IForcedSessionRefreshStore.RefreshAsync(bool force, CancellationToken cancellationToken) =>
      RefreshCoreAsync(force, reportTransientFailure: false, cancellationToken);

  Task IConfirmingSessionRefreshStore.RefreshConfirmingAsync(CancellationToken cancellationToken) =>
      RefreshCoreAsync(force: true, reportTransientFailure: true, cancellationToken);

  Task INativeLoginCredentialStore.InstallNativeLoginTokensAsync(
      string deviceToken, string sessionToken, CancellationToken cancellationToken) =>
      cookieJar.InstallNativeLoginTokensAsync(deviceToken, sessionToken, cancellationToken);

  public void ApplyCommittedOAuthDisconnect(OAuthBrokerProvider provider)
  {
    var identity = Current.Identity;
    if (identity is null) return;
    var disconnectedIdentity = provider switch
    {
      OAuthBrokerProvider.Facebook => identity with { FacebookAccount = null },
      OAuthBrokerProvider.X => identity with { XAccount = null },
      OAuthBrokerProvider.Github => identity with { GithubAccount = null },
      OAuthBrokerProvider.Linkedin => identity with { LinkedinAccount = null },
      OAuthBrokerProvider.Microsoft => identity with { MicrosoftAccount = null },
      OAuthBrokerProvider.Google => identity with { GoogleAccount = null },
      _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };
    SetCurrent(new SessionSnapshot(disconnectedIdentity));
  }

  private Task RefreshCoreAsync(
      bool force,
      bool reportTransientFailure,
      CancellationToken cancellationToken)
  {
    lock (syncLock)
    {
      if (signingOut && (!cookieJar.HasDeviceCookie || signOutGeneration is null || signOutGeneration == cookieJar.Generation))
        return Task.CompletedTask.WaitAsync(cancellationToken);
      var active = refreshTask is not null && !refreshTask.IsCompleted && refreshCookieGeneration == cookieJar.Generation;
      var identityStarted = identityRequestGeneration == refreshGeneration;
      if (active && (!force || !identityStarted))
      {
        return ObserveRefreshAsync(refreshTask!, reportTransientFailure, cancellationToken);
      }

      var rotate = !(active && identityStarted);
      refreshCookieGeneration = cookieJar.Generation;
      var generation = ++refreshGeneration;
      refreshTask = RefreshInternalAsync(generation, rotate, CancellationToken.None);
      return ObserveRefreshAsync(refreshTask, reportTransientFailure, cancellationToken);
    }
  }

  private async Task RefreshInternalAsync(
      long generation,
      bool rotate,
      CancellationToken cancellationToken)
  {
    var cookieGeneration = cookieJar.Generation;
    try
    {
      await cookieJar.RestoreAsync(cancellationToken).ConfigureAwait(false);
      if (rotate && cookieJar.CanRecoverAuthentication)
        await cookieJar.RenewAsync((dt, st, renewalToken) => client.SendAsync<NativeSessionRefreshResponse>(
            VouchaApiEndpoints.RefreshSession(dt!, st) with { IsCoordinatedSessionRotation = true }, renewalToken),
            cancellationToken: cancellationToken).ConfigureAwait(false);
      lock (syncLock)
      {
        if (generation != refreshGeneration || cookieGeneration != cookieJar.Generation) return;
        identityRequestGeneration = generation;
      }
      var response = await client
          .FetchMyIdentityAsync(cancellationToken)
          .ConfigureAwait(false);
      if (cookieGeneration == cookieJar.Generation) SetCurrent(generation, new SessionSnapshot(response.Identity));
    }
    catch (UnauthorizedAccessException)
    {
      if (cookieGeneration == cookieJar.Generation || !cookieJar.HasDeviceCookie)
        SetCurrent(generation, SessionSnapshot.Anonymous);
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
    {
      if (cookieGeneration == cookieJar.Generation || !cookieJar.HasDeviceCookie)
        SetCurrent(generation, SessionSnapshot.Anonymous);
    }
  }

  private static async Task ObserveRefreshAsync(Task refresh, bool reportTransientFailure, CancellationToken cancellationToken)
  {
    try
    {
      await refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (HttpRequestException) when (!reportTransientFailure)
    {
      // Preserve the current identity and cookies across transient transport failures.
    }
    catch (TaskCanceledException) when (!reportTransientFailure && !cancellationToken.IsCancellationRequested)
    {
      // A timed-out shared operation does not invalidate the current session.
    }
  }

  private void SetCurrent(SessionSnapshot snapshot)
  {
    lock (syncLock)
    {
      if (current == snapshot) return;
      current = snapshot;
    }

    SessionChanged?.Invoke(this, new SessionChangedEventArgs(snapshot));
  }

  private void SetCurrent(long generation, SessionSnapshot snapshot)
  {
    lock (syncLock)
    {
      if (generation != refreshGeneration || current == snapshot) return;
      current = snapshot;
    }

    SessionChanged?.Invoke(this, new SessionChangedEventArgs(snapshot));
  }
}

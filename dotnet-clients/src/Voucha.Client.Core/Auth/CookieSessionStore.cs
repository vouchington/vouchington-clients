using System.Net;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Auth;

#if DEBUG
public sealed class CookieSessionStore :
    ISessionStore,
    IForcedSessionRefreshStore,
    IConfirmingSessionRefreshStore,
    ICommittedOAuthDisconnectSessionStore,
    IDevelopmentSessionStore
#else
public sealed class CookieSessionStore :
    ISessionStore,
    IForcedSessionRefreshStore,
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

  public CookieSessionStore(
      VouchaApiClient client,
      SessionCookieJar cookieJar)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.cookieJar = cookieJar ?? throw new ArgumentNullException(nameof(cookieJar));
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

  public void ApplyCommittedOAuthDisconnect(OAuthBrokerProvider provider)
  {
    var identity = Current.Identity;
    if (identity is null) return;
    var disconnectedIdentity = provider switch
    {
      OAuthBrokerProvider.Facebook => identity with { FacebookAccount = null },
      OAuthBrokerProvider.X => identity with { XAccount = null },
      OAuthBrokerProvider.Github => identity with { GithubAccount = null },
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
      if (!force && refreshTask is not null && !refreshTask.IsCompleted)
      {
        return refreshTask;
      }

      var generation = force ? ++refreshGeneration : refreshGeneration;
      refreshTask = RefreshInternalAsync(generation, reportTransientFailure, cancellationToken);
      return refreshTask;
    }
  }

  private async Task RefreshInternalAsync(
      long generation,
      bool reportTransientFailure,
      CancellationToken cancellationToken)
  {
    try
    {
      var response = await client
          .FetchMyIdentityAsync(cancellationToken)
          .ConfigureAwait(false);
      SetCurrent(generation, new SessionSnapshot(response.Identity));
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
    {
      SetCurrent(generation, SessionSnapshot.Anonymous);
    }
    catch (VouchaApiException) when (!reportTransientFailure)
    {
      // Keep the prior snapshot for transient identity failures.
    }
    catch (HttpRequestException) when (!reportTransientFailure)
    {
      // Keep the prior snapshot for transient identity failures.
    }
    catch (TaskCanceledException) when (
        !reportTransientFailure && !cancellationToken.IsCancellationRequested)
    {
      // Keep the prior snapshot for transient identity failures.
    }
  }

  public async Task SignOutAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      await client
          .SendAsync(VouchaApiEndpoints.Logout(), cancellationToken)
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
      lock (syncLock)
      {
        refreshGeneration++;
      }

      using var clearTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      await cookieJar.ClearAsync(clearTimeout.Token).ConfigureAwait(false);
      SetCurrent(SessionSnapshot.Anonymous);
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
      if (generation != refreshGeneration) return;
    }

    SetCurrent(snapshot);
  }
}

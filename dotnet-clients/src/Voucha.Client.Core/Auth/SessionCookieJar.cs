using System.Net;
using System.Text.Json;

namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar : IDisposable
{
  private static readonly string[] SessionCookieNames = ["dt", "st"];
  private readonly CookieContainer cookieContainer;
  private readonly Uri apiBaseUrl;
  private readonly ISessionCookiePersistence persistence;
  private readonly TimeProvider renewalTimeProvider;
  private readonly SemaphoreSlim sync = new(1, 1);
  private volatile bool isRestored;
  private string? lastSerializedCookies;
  private bool? authenticatedSession;

  public SessionCookieJar(
      CookieContainer cookieContainer,
      Uri apiBaseUrl,
      ISessionCookiePersistence persistence,
      TimeProvider? timeProvider = null)
  {
    this.cookieContainer = cookieContainer ?? throw new ArgumentNullException(nameof(cookieContainer));
    this.apiBaseUrl = apiBaseUrl ?? throw new ArgumentNullException(nameof(apiBaseUrl));
    this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    renewalTimeProvider = timeProvider ?? TimeProvider.System;
  }

  public CookieContainer CookieContainer => cookieContainer;

  public async Task RestoreAsync(CancellationToken cancellationToken = default)
  {
    if (isRestored) return;

    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (isRestored) return;

      var serializedCookies = await persistence.ReadAsync(cancellationToken).ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(serializedCookies))
      {
        isRestored = true;
        return;
      }

      SessionCookieState? state;
      try
      {
        state = JsonSerializer.Deserialize<SessionCookieState>(serializedCookies);
      }
      catch (JsonException)
      {
        await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
        return;
      }

      if (state?.Cookies is null || !HasDeviceSession(state.Cookies))
      {
        await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
        return;
      }

      ClearMemoryUnlocked();
      foreach (var cookie in state.Cookies.Where(IsKnownCookie))
      {
        SetCookie(cookie.Name, cookie.Value, cookie.ExpiresUtc, cookie.Secure);
      }
      authenticatedSession = state.Authenticated;
      lastSerializedCookies = serializedCookies;
      isRestored = true;
    }
    finally
    {
      sync.Release();
    }
  }

  public async Task PersistAsync(CancellationToken cancellationToken = default)
  {
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }

  public async Task ClearAsync(CancellationToken cancellationToken = default)
  {
    Invalidate();
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }

  private void StoreSetCookieHeaders(Uri? requestUri, IEnumerable<string> setCookieHeaders)
  {
    ArgumentNullException.ThrowIfNull(setCookieHeaders);
    if (requestUri is null || !IsSameOrigin(requestUri)) return;

    foreach (var header in setCookieHeaders)
    {
      try
      {
        cookieContainer.SetCookies(apiBaseUrl, header);
      }
      catch (CookieException)
      {
        // Ignore malformed Set-Cookie headers instead of failing the API response.
      }
    }
  }

  public void Dispose() => sync.Dispose();

  private async Task ClearUnlockedAsync(CancellationToken cancellationToken)
  {
    ClearMemoryUnlocked();
    Interlocked.Increment(ref credentialRevision);
    authenticatedSession = null;
    await persistence.ClearAsync(cancellationToken).ConfigureAwait(false);
    lastSerializedCookies = null;
    isRestored = true;
  }

  private async Task PersistUnlockedAsync(CancellationToken cancellationToken)
  {
    var cookies = SessionCookieNames
        .Select(ReadCookie)
        .OfType<SessionCookie>()
        .ToArray();
    if (!HasDeviceSession(cookies))
    {
      await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
      return;
    }

    var state = new SessionCookieState(cookies, authenticatedSession);
    var serialized = JsonSerializer.Serialize(state);
    if (serialized == lastSerializedCookies)
    {
      isRestored = true;
      return;
    }

    await persistence
        .WriteAsync(serialized, cancellationToken)
        .ConfigureAwait(false);
    lastSerializedCookies = serialized;
    isRestored = true;
  }

  private void ClearMemoryUnlocked()
  {
    foreach (var name in SessionCookieNames)
    {
      ExpireCookie(name);
    }
  }

}

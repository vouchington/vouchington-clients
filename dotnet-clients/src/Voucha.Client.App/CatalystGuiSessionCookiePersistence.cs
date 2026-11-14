#if DEBUG
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

internal sealed class CatalystGuiSessionCookiePersistence : ISessionCookiePersistence
{
  private readonly object gate = new();
  private string? snapshot;

  internal static ISessionCookiePersistence Select(
      bool validatedFixtureMode, Func<ISessionCookiePersistence> productionFactory)
  {
    ArgumentNullException.ThrowIfNull(productionFactory);
    return validatedFixtureMode ? new CatalystGuiSessionCookiePersistence() : productionFactory();
  }

  public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    lock (gate) return Task.FromResult(snapshot);
  }

  public Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    lock (gate) snapshot = serializedCookies;
    return Task.CompletedTask;
  }

  public Task ClearAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    lock (gate) snapshot = null;
    return Task.CompletedTask;
  }
}
#endif

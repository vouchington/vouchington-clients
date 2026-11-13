using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.HackerNews;

/// <summary>
/// Tracks the backend's Hacker News reference flag (<c>/api/v1/hacker-news-config</c>).
/// Fails closed: references stay hidden until a successful fetch reports them visible, and a
/// failed refresh keeps the last successfully fetched value (hidden when none exists).
/// </summary>
public sealed class HackerNewsReferenceGate
{
  private readonly Func<CancellationToken, Task<HackerNewsConfigResponse>> fetch;
  private readonly object listenersGate = new();
  private readonly Dictionary<long, WeakReference<IHackerNewsReferenceListener>> listeners = [];
  private long nextListenerId;
  private volatile bool referencesVisible;

  public HackerNewsReferenceGate(Func<CancellationToken, Task<HackerNewsConfigResponse>> fetch) =>
      this.fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));

  public bool ReferencesVisible => referencesVisible;

  public event EventHandler? Changed;

  /// <summary>
  /// Subscribes weakly so a page's view model that is never disposed is not kept alive by this
  /// app-lifetime gate; disposing the returned handle removes the subscription.
  /// </summary>
  public IDisposable Subscribe(IHackerNewsReferenceListener listener)
  {
    ArgumentNullException.ThrowIfNull(listener);
    var id = Interlocked.Increment(ref nextListenerId);
    lock (listenersGate) listeners.Add(id, new WeakReference<IHackerNewsReferenceListener>(listener));
    return new Subscription(this, id);
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "A failed flag fetch must leave the filter hidden instead of failing the feed load that triggered it.")]
  public async Task RefreshAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      var config = await fetch(cancellationToken).ConfigureAwait(true);
      Set(config.ReferencesVisible);
    }
    catch (OperationCanceledException)
    {
      // Superseded or canceled: keep the last known value.
    }
    catch (Exception)
    {
      // Keep the last known value; it is hidden until a fetch has succeeded.
    }
  }

  private void Set(bool value)
  {
    if (referencesVisible == value) return;
    referencesVisible = value;
    Changed?.Invoke(this, EventArgs.Empty);
    foreach (var listener in LiveListeners()) listener.OnHackerNewsReferencesChanged();
  }

  private List<IHackerNewsReferenceListener> LiveListeners()
  {
    lock (listenersGate)
    {
      var live = new List<IHackerNewsReferenceListener>(listeners.Count);
      foreach (var (id, reference) in listeners.ToList())
      {
        if (reference.TryGetTarget(out var listener)) live.Add(listener);
        else listeners.Remove(id);
      }
      return live;
    }
  }

  private void Unsubscribe(long id)
  {
    lock (listenersGate) listeners.Remove(id);
  }

  private sealed class Subscription(HackerNewsReferenceGate gate, long id) : IDisposable
  {
    private int disposed;

    public void Dispose()
    {
      if (Interlocked.Exchange(ref disposed, 1) == 0) gate.Unsubscribe(id);
    }
  }
}

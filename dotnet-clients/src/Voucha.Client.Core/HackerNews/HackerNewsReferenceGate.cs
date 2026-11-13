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
  private volatile bool referencesVisible;

  public HackerNewsReferenceGate(Func<CancellationToken, Task<HackerNewsConfigResponse>> fetch) =>
      this.fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));

  public bool ReferencesVisible => referencesVisible;

  public event EventHandler? Changed;

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
  }
}

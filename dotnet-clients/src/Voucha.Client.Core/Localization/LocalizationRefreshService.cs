using System.Collections.Concurrent;
using System.Net.Http;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Localization;

public sealed class LocalizationRefreshService(
    VouchaApiClient client,
    IUiLocaleController localeController,
    LocalizationValueCache cache)
{
  private readonly ConcurrentDictionary<string, SemaphoreSlim> refreshGates = new(StringComparer.Ordinal);

  public async Task RefreshChromeAsync(CancellationToken cancellationToken = default)
  {
    var locale = localeController.EffectiveLocale;
    var gate = refreshGates.GetOrAdd(locale, static _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (!cache.IsExpired(locale, DateTimeOffset.UtcNow)) return;
      var batch = await client.FetchLocalizationAsync(
          NativeLocalizationSelectors.Consumer,
          locale,
          NativeLocalizationSelectors.ChromeJoined,
          cache.Etag(locale),
          cancellationToken).ConfigureAwait(false);
      var now = DateTimeOffset.UtcNow;
      if (batch is null)
      {
        cache.RememberNotModified(locale, now);
        return;
      }
      cache.Apply(
          locale,
          batch.Revision,
          batch.TtlSeconds,
          LocalizationLeafFlatten.Flatten(batch.Messages),
          now);
      localeController.NotifyLocalizedCopyChanged();
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (HttpRequestException)
    {
    }
    finally
    {
      gate.Release();
    }
  }
}

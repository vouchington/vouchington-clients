using System.Net.Http;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Localization;

public sealed class LocalizationRefreshService(
    VouchaApiClient client,
    IUiLocaleController localeController,
    LocalizationValueCache cache)
{
  public async Task RefreshChromeAsync(CancellationToken cancellationToken = default)
  {
    var locale = localeController.EffectiveLocale;
    if (!cache.IsExpired(locale, DateTimeOffset.UtcNow)) return;
    try
    {
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
  }
}

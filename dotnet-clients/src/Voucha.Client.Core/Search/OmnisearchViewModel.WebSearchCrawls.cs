using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private IReadOnlyList<OmnisearchResultGroup> MapUrlCrawlListGroups(
      string webAddressId,
      UrlCrawlsResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    var rows = (response.Results ?? [])
        .Select(crawl => CrawlRow(
            webAddressId,
            crawl,
            crawl.Title
                ?? localization.FormatDateTime(crawl.CreatedAt, TimeZoneInfo.Local)))
        .ToArray();

    return rows.Length == 0 ? [] : [new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualCrawlHistory), rows)];
  }

  private IReadOnlyList<OmnisearchResultGroup> MapUrlCrawlDetailGroups(UrlCrawlResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);
    if (response.Crawl is null) return [];

    var rows = new List<OmnisearchResultRow>
    {
      new(L(localization, UiMessageKey.NativeDotnetResidualCrawl), response.Crawl.Id, CrawlStatus(response.Crawl)),
      new(L(localization, UiMessageKey.NativeDotnetResidualTitle), response.Crawl.Title ?? L(localization, UiMessageKey.NativeDotnetResidualUntitledCrawl), response.Crawl.Lang ?? L(localization, UiMessageKey.NativeDotnetResidualNoLanguage)),
    };
    if (response.Crawl.Markdown is not null)
    {
      rows.Add(new(L(localization, UiMessageKey.NativeDotnetResidualMarkdown), response.Crawl.Markdown, L(localization, UiMessageKey.NativeDotnetResidualCrawlerOutput)));
    }

    return [new OmnisearchResultGroup(L(localization, UiMessageKey.NativeDotnetResidualCrawlDetail), rows)];
  }

  private OmnisearchResultRow CrawlRow(string urlId, Crawl crawl, string title) =>
      new(L(localization, UiMessageKey.NativeDotnetResidualCrawl), title, CrawlStatus(crawl), OmnisearchResultRoute.CrawlRecord(urlId, crawl.Id));

  private string CrawlStatus(Crawl crawl) =>
      crawl.CompletedAt is null
          ? L(localization, UiMessageKey.NativeDotnetResidualPending)
          : crawl.ResponseStatusCode is int statusCode
              ? F(localization, UiMessageKey.NativeDotnetResidualCompletedHttp, ("status", statusCode))
              : L(localization, UiMessageKey.NativeDotnetResidualCompleted);
}

using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Fact]
  public async Task PaidSafeCrawlDetailDoesNotPresentMissingMarkdownAsEmptyContent()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(PaidUrlCrawlDetailJson),
      new RecordedResponse(UrlJson),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: AuthenticatedViewerProvider());

    await viewModel.LoadUrlCrawlDetailAsync("url-1", "crawl-1", TestContext.Current.CancellationToken);

    Assert.DoesNotContain(viewModel.Groups.SelectMany(group => group.Rows), row => row.Title == "Markdown");
  }

  private const string PaidUrlCrawlDetailJson = """
      { "crawl": { "__entity_type": "crawl", "id": "crawl-1", "url_id": "url-1",
        "created_at": "2026-07-01T00:00:00Z", "completed_at": "2026-07-01T00:01:00Z",
        "response_status_code": 200, "title": "Native result", "lang": "en" } }
      """;

  private const string UrlJson = """
      { "url": { "id": "url-1", "url": "https://example.com/a", "pathname": "/a",
        "hostname": { "id": "hostname-1", "hostname": "example.com", "is_blocked": false } },
        "latest_crawl": null, "can_view_latest_crawl": true, "can_view_crawl_history": true,
        "can_trigger_crawl": false, "url_type": "web", "rss_feed_id": null }
      """;
}

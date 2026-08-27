using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Fact]
  public async Task ViewerChangeDisablesLoadedUrlCrawlTrigger()
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, ["administrator"]));
    var handler = new RecordingHandler(AdminUrlDetailJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: viewerProvider);

    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);
    viewerProvider.SetViewer(NavigationViewer.Anonymous);
    await viewModel.TriggerSelectedUrlCrawlAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasSelectedUrl);
    Assert.False(viewModel.CanTriggerSelectedUrlCrawl);
    Assert.Single(handler.Requests);
  }

  private const string AdminUrlDetailJson = """
      {
        "url": {
          "__entity_type": "url",
          "id": "url-1",
          "url": "https://example.com/native",
          "pathname": "/native",
          "hostname": {
            "__entity_type": "hostname",
            "id": "hostname-1",
            "hostname": "example.com"
          }
        },
        "latest_crawl": null,
        "can_view_latest_crawl": true,
        "can_view_crawl_history": true,
        "can_trigger_crawl": true,
        "url_type": "web",
        "rss_feed_id": null
      }
      """;
}

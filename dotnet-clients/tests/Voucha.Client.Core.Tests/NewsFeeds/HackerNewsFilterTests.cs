using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class HackerNewsFilterTests
{
  [Fact]
  public async Task PodcastFilterReloadsFirstPageAndCarriesFilterAcrossContinuation()
  {
    var handler = new PageHandler(Page("first"), Page("filtered"), Page(null));
    var service = new ApiNewsFeedService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    var viewModel = new NewsFeedsViewModel(service, NewsFeedKind.Podcasts, NewsFeedScope.AllPodcasts);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.ToggleHackerNewsFilterAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMore);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Collection(handler.Paths,
        path => ApiQueryAssertions.Equal("/api/v1/rss-feed-items?limit=20&media_type=audio", path),
        path => ApiQueryAssertions.Equal("/api/v1/rss-feed-items?limit=20&media_type=audio&has_hacker_news_references=true", path),
        path => ApiQueryAssertions.Equal("/api/v1/rss-feed-items?limit=20&after=filtered&media_type=audio&has_hacker_news_references=true", path));
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task UnsupportedFeedServiceFailsClosedWhenFilterIsSelected()
  {
    var service = new SampleNewsFeedService();

    await Assert.ThrowsAsync<NotSupportedException>(() =>
        ((INewsFeedService)service).GetNewsFeedPageAsync(
            NewsFeedScope.AllPodcasts,
            after: null,
            limit: 20,
            hasHackerNewsReferences: true,
            cancellationToken: TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task PodcastFilterCanRequestUrlsWithoutHackerNewsReferences()
  {
    var handler = new PageHandler(Page(null));
    var service = new ApiNewsFeedService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await service.GetNewsFeedPageAsync(
        NewsFeedScope.AllPodcasts,
        after: null,
        limit: 20,
        hasHackerNewsReferences: false,
        cancellationToken: TestContext.Current.CancellationToken);

    ApiQueryAssertions.Equal("/api/v1/rss-feed-items?limit=20&media_type=audio&has_hacker_news_references=false",
        Assert.Single(handler.Paths));
  }

  private static string Page(string? cursor) => JsonSerializer.Serialize(new
  {
    results = Array.Empty<object>(),
    page_info = new { has_next_page = cursor is not null, end_cursor = cursor },
    rss_feed_items = new Dictionary<string, object>(),
  });

  private sealed class PageHandler(params string[] pages) : HttpMessageHandler
  {
    private readonly Queue<string> responses = new(pages);

    public List<string> Paths { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(responses.Dequeue()),
      });
    }
  }
}

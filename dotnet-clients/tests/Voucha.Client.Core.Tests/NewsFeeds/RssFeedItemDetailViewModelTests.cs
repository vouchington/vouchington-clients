using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class RssFeedItemDetailViewModelTests
{
  private const string FixtureItemId = "00000000-0000-7000-8000-000000007886";

  [Fact]
  public async Task ApiServiceMapsFocusedItemSidecars()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.rss-feed-item.detail.default"));
    IRssFeedItemDetailService service = new ApiNewsFeedService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));

    var detail = await service.FetchAsync(FixtureItemId, NewsFeedItemKind.Media, TestContext.Current.CancellationToken);

    Assert.Equal($"/api/v1/rss-feed-items/{FixtureItemId}", handler.PathAndQuery);
    Assert.Equal("Test Article", detail.Item.Title);
    Assert.True(detail.Item.IsSaved);
    Assert.Equal(5, detail.Item.VoteScoreNet);
    Assert.Equal(ElectionVoteChoice.Like, detail.Item.CurrentVoteChoice);
    Assert.Equal(new Uri("https://images.example.test/rss-feed-item-detail.jpg"), detail.Item.ThumbnailUrl);
    Assert.Equal("<p>A short snippet</p>", detail.ContentHtml);
  }

  [Fact]
  public async Task LoadAsyncPresentsFocusedItemAndSupportsRetry()
  {
    var service = new StubService(new InvalidOperationException("Unavailable"));
    var viewModel = new RssFeedItemDetailViewModel(service, "item-1", NewsFeedItemKind.Article);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Unavailable", viewModel.ErrorMessage);

    service.Error = null;
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasDetail);
    Assert.Equal("Focused story", viewModel.Detail?.Item.Title);
    Assert.Null(viewModel.ErrorMessage);
  }

  private sealed class StubService(Exception? error) : IRssFeedItemDetailService
  {
    public Exception? Error { get; set; } = error;

    public Task<RssFeedItemDetail> FetchAsync(string itemId, NewsFeedItemKind kind, CancellationToken cancellationToken = default)
    {
      if (Error is not null) return Task.FromException<RssFeedItemDetail>(Error);
      return Task.FromResult(new RssFeedItemDetail(
          new NewsFeedItem(itemId, "Focused story", "Source", "Summary", null, DateTimeOffset.UtcNow, kind),
          "<p>Summary</p>"));
    }
  }
}

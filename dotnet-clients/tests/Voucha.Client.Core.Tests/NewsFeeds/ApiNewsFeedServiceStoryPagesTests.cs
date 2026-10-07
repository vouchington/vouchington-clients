using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class ApiNewsFeedServiceStoryPagesTests
{
  [Fact]
  public async Task MapsBoundedPreviewAndPageSidecarsAndForwardsExactCursor()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(Feed()), new RecordedResponse(Page()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiNewsFeedService(client);
    var feed = await service.GetNewsFeedPageAsync(NewsFeedScope.AllNews, cancellationToken: TestContext.Current.CancellationToken);
    var primary = Assert.Single(feed.Items);
    var preview = Assert.Single(Assert.IsType<StoryRelatedArticles>(primary.StoryArticles).Items);
    Assert.Equal("peer-3", preview.Id);
    Assert.True(preview.IsSaved);
    Assert.Equal(ElectionVoteChoice.Like, preview.CurrentVoteChoice);
    Assert.Equal(4, preview.VoteCountUp);
    Assert.Equal(new Uri("/peer.jpg", UriKind.Relative), preview.ThumbnailUrl);

    var page = await service.GetStoryRelatedArticlesPageAsync("story/one", "primary/one", "opaque+/=", TestContext.Current.CancellationToken);
    Assert.Equal(["peer-2"], page.Items.Select(item => item.Id));
    Assert.False(page.PageInfo.HasNextPage);
    Assert.Equal(HttpMethod.Get, handler.Method);
    var request = Assert.IsType<string>(handler.PathAndQuery);
    Assert.StartsWith("/api/v1/stories/story%2Fone?", request, StringComparison.Ordinal);
    Assert.Contains("after=opaque%2B%2F%3D", request, StringComparison.Ordinal);
    Assert.Contains("exclude_item_id=primary%2Fone", request, StringComparison.Ordinal);
    Assert.Contains("limit=25", request, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SharedDeliveriesNeverAcquireStoryPreviews()
  {
    var handler = new RecordingHandler(Feed(shared: true));
    var service = new ApiNewsFeedService(new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    var items = await service.GetNewsFeedItemsAsync(NewsFeedScope.AllNews, TestContext.Current.CancellationToken);
    var item = Assert.Single(items);
    Assert.Null(item.StoryId);
    Assert.Null(item.StoryArticles);
    Assert.False(item.CanStartStoryDiscussion);
  }

  [Fact]
  public async Task PreviewArticleActionsUpdatePeersAndRetainPrimaryOnFailure()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(Feed()), new RecordedResponse("{}", HttpStatusCode.NoContent),
      new RecordedResponse("{}", HttpStatusCode.NoContent), new RecordedResponse("offline", HttpStatusCode.ServiceUnavailable),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var model = new NewsFeedsViewModel(new ApiNewsFeedService(client), NewsFeedScope.AllNews, new ApiBookmarkService(client));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var primary = Assert.Single(model.Items);
    var group = Assert.IsType<StoryRelatedArticles>(primary.StoryArticles);
    await model.VoteRssFeedItemAsync(group.Items[0], ElectionVoteChoice.Dislike, TestContext.Current.CancellationToken);
    Assert.Equal(ElectionVoteChoice.Dislike, group.Items[0].CurrentVoteChoice);
    Assert.Equal(3, group.Items[0].VoteCountUp);
    Assert.Equal(2, group.Items[0].VoteCountDown);
    await model.ToggleSaveAsync(group.Items[0], TestContext.Current.CancellationToken);
    Assert.False(group.Items[0].IsSaved);
    await model.ToggleHideAsync(group.Items[0], TestContext.Current.CancellationToken);
    Assert.Equal("peer-3", Assert.Single(group.Items).Id);
    Assert.Same(primary, Assert.Single(model.Items));
    Assert.True(model.HasError);
    Assert.Equal(["/api/v1/rss-feed-items", "/api/v1/rss-feed-items/peer-3/vote", "/api/v1/bookmarks/rss_feed_item/peer-3/save", "/api/v1/bookmarks/rss_feed_item/peer-3/hide"],
        handler.Requests.Select(request => new Uri("https://api.test" + request.PathAndQuery).AbsolutePath));
  }

  private static string Feed(bool shared = false) => JsonSerializer.Serialize(new
  {
    results = new[] { new { id = "primary", entity_id = "primary", story_id = "story-1", delivery_type = shared ? "share" : "direct" } },
    page_info = new { has_next_page = false },
    story_member_pages = new Dictionary<string, object> { ["story-1"] = new { item_ids = new[] { "peer-3" }, page_info = new { has_next_page = true, end_cursor = "opaque+/=" } } },
    rss_feed_items = new Dictionary<string, object> { ["primary"] = Article("primary"), ["peer-3"] = Article("peer-3") },
    rss_feed_item_elections = new Dictionary<string, object> { ["peer-3"] = new { votes_score_net = 3, votes_count_up = 4, votes_count_down = 1 } },
    election_votes = new Dictionary<string, object> { ["peer-3"] = new { user_id = "viewer", entity_id = "peer-3", choice = "like", created_at = "2026-01-01T00:00:00Z" } },
    bookmarks = new Dictionary<string, object> { ["peer-3"] = new { save = true } },
    rss_feed_item_thumbnail_url = new Dictionary<string, string> { ["peer-3"] = "/peer.jpg" },
  });

  private static string Page() => JsonSerializer.Serialize(new
  {
    story = new { id = "story/one", created_at = "2026-01-01T00:00:00Z", updated_at = "2026-01-01T00:00:00Z" },
    item_ids = new[] { "peer-2", "primary/one" },
    page_info = new { has_next_page = false },
    rss_feed_items = new Dictionary<string, object> { ["peer-2"] = Article("peer-2"), ["primary/one"] = Article("primary/one") },
  });

  private static object Article(string id) => new { id, title = id, published_at = "2026-01-01T00:00:00Z" };
}

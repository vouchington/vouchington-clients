using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Content;

public sealed class HackerNewsReferenceMappingTests
{
  [Theory]
  [InlineData("native.rss-feed-items.hacker-news.list", NewsFeedScope.AllNews)]
  [InlineData("native.podcast-episodes.hacker-news.list", NewsFeedScope.AllPodcasts)]
  public async Task RssAndPodcastCardsKeepStoredCounts(string fixture, NewsFeedScope scope)
  {
    var service = new ApiNewsFeedService(Client(ApiFixtureLoader.LoadResponse(fixture)));
    var row = Assert.Single(await service.GetNewsFeedItemsAsync(scope, TestContext.Current.CancellationToken));
    Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", row.HackerNews.Summary);
    Assert.Empty(row.HackerNews.Comments);
  }

  [Fact]
  public async Task RssAndPodcastDetailsKeepStoredLinks()
  {
    var service = new ApiNewsFeedService(Client(ApiFixtureLoader.LoadResponse("native.rss-feed-items.hacker-news.detail")));
    foreach (var kind in new[] { NewsFeedItemKind.Article, NewsFeedItemKind.Media })
    {
      var detail = await service.FetchAsync("item-1", kind, TestContext.Current.CancellationToken);
      Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", detail.Item.HackerNews.Summary);
      Assert.Single(detail.Item.HackerNews.Stories);
      Assert.Equal(2, detail.Item.HackerNews.Comments.Count);
    }
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task PostCardsKeepCountsAcrossFeedAndBrowse(bool browse)
  {
    using var model = new PostsListViewModel(new ApiPostsService(Client(File.ReadAllText(ContractPaths.ApiFixture("responses/native.posts.hacker-news.filtered.json")))));
    if (browse) await model.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken);
    else await model.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", Assert.Single(model.Items).HackerNews.Summary);
  }

  [Fact]
  public async Task PostDetailRetainsTheRootReferenceEnvelope()
  {
    var detail = ApiFixtureLoader.LoadResponse("native.posts.hacker-news.detail");
    var descendants = """{"results":[],"posts":{},"users":{},"communities":{},"page_info":{"has_next_page":false}}""";
    var client = new VouchaApiClient(new HttpClient(new Handler(request =>
        request.RequestUri!.AbsolutePath.EndsWith("/descendants", StringComparison.Ordinal) ? descendants : detail))
    { BaseAddress = new Uri("https://example.com") });
    var model = new CommentThreadViewModel(new ApiPostsService(client), "profile-review-1");
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(model.RootHackerNewsReferences);
    Assert.Equal(2, model.RootHackerNewsReferences.Comments!.Count);
  }

  [Fact]
  public async Task PostPaginationRetainsEachPagesReferenceCounts()
  {
    var first = JsonNode.Parse(File.ReadAllText(ContractPaths.ApiFixture("responses/native.posts.hacker-news.filtered.json")))!;
    first["page_info"]!["has_next_page"] = true;
    first["page_info"]!["end_cursor"] = "next";
    var second = first.ToJsonString().Replace("profile-review-1", "second-post", StringComparison.Ordinal);
    var client = new VouchaApiClient(new HttpClient(new Handler(request =>
        request.RequestUri!.Query.Contains("after=next", StringComparison.Ordinal) ? second : first.ToJsonString()))
    { BaseAddress = new Uri("https://example.com") });
    using var model = new PostsListViewModel(new ApiPostsService(client));
    await model.LoadBrowseAsync(cancellationToken: TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["profile-review-1", "second-post"], model.Items.Select(row => row.Id));
    Assert.All(model.Items, row => Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", row.HackerNews.Summary));
  }

  private static VouchaApiClient Client(string json) => new(new HttpClient(new Handler(_ => json)) { BaseAddress = new Uri("https://example.com") });

  private sealed class Handler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(request), Encoding.UTF8, "application/json") });
  }
}

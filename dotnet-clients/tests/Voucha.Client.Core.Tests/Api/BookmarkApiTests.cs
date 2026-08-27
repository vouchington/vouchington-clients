using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Profiles;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class BookmarkApiTests
{
  [Fact]
  public async Task ApiBookmarkServiceUsesPutAndDeleteBookmarkEndpoints()
  {
    var handler = new RecordingHandler(string.Empty, HttpStatusCode.NoContent);
    var service = new ApiBookmarkService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }));

    await service.SetAsync(
        "rss_feed_item",
        "item 1",
        BookmarkPredicate.Hide,
        true,
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Put, handler.Method);
    Assert.Equal("/api/v1/bookmarks/rss_feed_item/item%201/hide", handler.PathAndQuery);

    await service.SetAsync(
        "rss_feed_item",
        "item 1",
        BookmarkPredicate.Hide,
        false,
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Delete, handler.Method);
    Assert.Equal("/api/v1/bookmarks/rss_feed_item/item%201/hide", handler.PathAndQuery);
  }

  [Fact]
  public void ApiBookmarkServiceRejectsNullClient()
  {
    Assert.Throws<ArgumentNullException>(() => new ApiBookmarkService(null!));
  }

  [Fact]
  public async Task NullBookmarkServiceCompletesWithoutSendingRequests()
  {
    await NullBookmarkService.Instance.SetAsync(
        "topic",
        "topic-1",
        BookmarkPredicate.Mute,
        true,
        TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task ApiProfileSafetyServiceFetchesUserBookmarks()
  {
    const string ResponseJson = """
        {
          "bookmarks": {
            "mute": true,
            "block": false
          }
        }
        """;
    var handler = new RecordingHandler(ResponseJson);
    var service = new ApiProfileSafetyService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }),
        new RecordingBookmarkService());

    var bookmarks = await service.FetchUserBookmarksAsync("user 1", TestContext.Current.CancellationToken);

    Assert.True(bookmarks.IsActive(BookmarkPredicate.Mute));
    Assert.False(bookmarks.IsActive(BookmarkPredicate.Block));
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal("/api/v1/bookmarks/user/user%201", handler.PathAndQuery);
  }

  [Fact]
  public async Task ApiProfileSafetyServiceDefaultsMissingUserBookmarks()
  {
    var handler = new RecordingHandler("{}");
    var service = new ApiProfileSafetyService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }),
        new RecordingBookmarkService());

    var bookmarks = await service.FetchUserBookmarksAsync("user 1", TestContext.Current.CancellationToken);

    Assert.False(bookmarks.IsActive(BookmarkPredicate.Mute));
    Assert.False(bookmarks.IsActive(BookmarkPredicate.Block));
  }

  [Fact]
  public async Task ApiProfileSafetyServiceDelegatesUserBookmarkMutations()
  {
    var bookmarks = new RecordingBookmarkService();
    var service = new ApiProfileSafetyService(
        new VouchaApiClient(new HttpClient(new RecordingHandler(string.Empty))
        {
          BaseAddress = new Uri("https://api.example.test"),
        }),
        bookmarks);

    await service.SetUserBookmarkAsync(
        "user 1",
        BookmarkPredicate.Block,
        true,
        TestContext.Current.CancellationToken);

    Assert.Equal(
        [("user", "user 1", BookmarkPredicate.Block, true)],
        bookmarks.Calls);
  }

  [Fact]
  public async Task ApiProfileSafetyServiceReportsUsers()
  {
    var handler = new RecordingHandler(string.Empty, HttpStatusCode.NoContent);
    var service = new ApiProfileSafetyService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") }),
        new RecordingBookmarkService());

    await service.ReportUserAsync(
        "user 1",
        "spam",
        "duplicate account",
        "turnstile-token",
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/reports", handler.PathAndQuery);
    Assert.Contains("\"entityType\":\"user\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"entityId\":\"user 1\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"reason\":\"spam\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"note\":\"duplicate account\"", handler.RequestBody, StringComparison.Ordinal);
    Assert.Contains("\"cf_turnstile_response\":\"turnstile-token\"", handler.RequestBody, StringComparison.Ordinal);
  }

  [Fact]
  public void ApiProfileSafetyServiceRejectsNullDependencies()
  {
    var client = new VouchaApiClient(new HttpClient(new RecordingHandler(string.Empty))
    {
      BaseAddress = new Uri("https://api.example.test"),
    });

    Assert.Throws<ArgumentNullException>(() => new ApiProfileSafetyService(null!, new RecordingBookmarkService()));
    Assert.Throws<ArgumentNullException>(() => new ApiProfileSafetyService(client, null!));
  }

  [Theory]
  [InlineData(BookmarkPredicate.Save, "save")]
  [InlineData(BookmarkPredicate.Hide, "hide")]
  [InlineData(BookmarkPredicate.Follow, "follow")]
  [InlineData(BookmarkPredicate.Mute, "mute")]
  [InlineData(BookmarkPredicate.Block, "block")]
  [InlineData(BookmarkPredicate.Subscribe, "subscribe")]
  [InlineData(BookmarkPredicate.DismissRecommendation, "dismiss_recommendation")]
  [InlineData(BookmarkPredicate.ProxyFollow, "proxy_follow")]
  [InlineData(BookmarkPredicate.ProxyMute, "proxy_mute")]
  public void BookmarkPredicatesMapToApiNames(BookmarkPredicate predicate, string apiName)
  {
    Assert.Equal(apiName, predicate.ToApiName());
  }

  [Fact]
  public void BookmarkPredicatesRejectUnknownPredicate()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => ((BookmarkPredicate)999).ToApiName());
    Assert.Throws<ArgumentOutOfRangeException>(() => new BookmarkPredicates().IsActive((BookmarkPredicate)999));
  }

  [Fact]
  public void BookmarkSidecarHandlesNullMissingAndActiveEntries()
  {
    var sidecar = new Dictionary<string, BookmarkPredicates>(StringComparer.Ordinal)
    {
      ["entity-1"] = new(Save: true, Mute: false, ProxyFollow: true),
    };

    Assert.True(BookmarkSidecar.IsActive(sidecar, "entity-1", BookmarkPredicate.Save));
    Assert.False(BookmarkSidecar.IsActive(sidecar, "entity-1", BookmarkPredicate.Mute));
    Assert.True(BookmarkSidecar.IsActive(sidecar, "entity-1", BookmarkPredicate.ProxyFollow));
    Assert.False(BookmarkSidecar.IsActive(sidecar, "entity-1", BookmarkPredicate.ProxyMute));
    Assert.False(BookmarkSidecar.IsActive(sidecar, "missing", BookmarkPredicate.Save));
    Assert.False(BookmarkSidecar.IsActive(sidecar, null, BookmarkPredicate.Save));
    Assert.False(BookmarkSidecar.IsActive(null, "entity-1", BookmarkPredicate.Save));
  }

  [Fact]
  public async Task BookmarkReferenceHelpersUseCollectionEndpoints()
  {
    const string ResponseJson = """
        {
          "results": [{ "id": "entity-1" }],
          "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
        }
        """;
    var handler = new RecordingHandler(Enumerable.Repeat(new RecordedResponse(ResponseJson), 5));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

    var posts = await client.FetchUserPostBookmarkReferencesAsync(
        "user 1",
        "saved",
        "review",
        "cursor 1",
        7,
        TestContext.Current.CancellationToken);
    var topics = await client.FetchUserTopicBookmarkReferencesAsync(
        "user 1",
        "following",
        "cursor 2",
        8,
        TestContext.Current.CancellationToken);
    var users = await client.FetchUserUserBookmarkReferencesAsync(
        "user 1",
        "muted",
        "cursor 3",
        9,
        TestContext.Current.CancellationToken);
    var communities = await client.FetchUserCommunityBookmarkReferencesAsync(
        "user 1",
        "proxy-following",
        "cursor 4",
        10,
        TestContext.Current.CancellationToken);
    var rssFeedItems = await client.FetchUserRssFeedItemBookmarkReferencesAsync(
        "user 1",
        "saved",
        "video",
        "cursor 5",
        11,
        TestContext.Current.CancellationToken);

    Assert.All(
        new[] { posts, topics, users, communities, rssFeedItems },
        response => Assert.Equal("entity-1", response.Results[0].Id));
    Assert.Equal(
        [
          "/api/v1/users/user%201/posts/saved?after=cursor%201&limit=7&media_type=review",
          "/api/v1/users/user%201/topics/following?after=cursor%202&limit=8",
          "/api/v1/users/user%201/users/muted?after=cursor%203&limit=9",
          "/api/v1/users/user%201/communities/proxy-following?after=cursor%204&limit=10",
          "/api/v1/users/user%201/rss-feed-items/saved?after=cursor%205&limit=11&media_type=video",
        ],
        handler.Requests.Select(request => request.PathAndQuery));
  }

  [Fact]
  public void EntityBookmarksEndpointEscapesRouteSegments()
  {
    var request = VouchaApiEndpoints.EntityBookmarks("user type", "user/1");

    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/bookmarks/user%20type/user%2F1", request.Path);
  }

  private sealed class RecordingBookmarkService : IBookmarkService
  {
    public List<(string EntityType, string EntityId, BookmarkPredicate Predicate, bool Active)> Calls { get; } = [];

    public Task SetAsync(
        string entityType,
        string entityId,
        BookmarkPredicate predicate,
        bool active,
        CancellationToken cancellationToken = default)
    {
      Calls.Add((entityType, entityId, predicate, active));
      return Task.CompletedTask;
    }
  }
}

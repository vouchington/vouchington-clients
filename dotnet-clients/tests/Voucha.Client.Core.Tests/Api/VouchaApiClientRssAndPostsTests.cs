using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchRssFeedItemsAsyncUsesWebSharedFixture()
  {
    var (client, handler) = CreateClient("web.rss-feed-items.feed.default");

    var response = await client.FetchRssFeedItemsAsync(
        new FetchRssFeedItemsRequest(),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/feeds/rss_feed_items/any?limit=20");
    Assert.Equal("item-1", response.Results[0].Id);
    Assert.Equal("Test Article", response.RssFeedItems["item-1"].Data?.Title);
    Assert.Equal("video", response.RssFeedItems["item-1"].MediaType);
    Assert.Equal("feed-1", response.RssFeedItems["item-1"].RssFeed?.Id);
    var hashtag = response.RssFeedItems["item-peer"].Categories?.Single().Hashtag;
    Assert.Equal("00000000-0000-7000-8000-000000000101", hashtag?.Id);
    Assert.Equal("travel", hashtag?.Key);
    Assert.Equal("#Travel", hashtag?.DisplayToken);
    Assert.Equal("topic-1", hashtag?.TopicId);
  }

  [Fact]
  public async Task FetchRssFeedItemsAsyncUsesSwiftSharedFixtureWithMediaType()
  {
    var (client, handler) = CreateClient("swift.rss-feed-items.feed.default");

    var response = await client.FetchRssFeedItemsAsync(
        new FetchRssFeedItemsRequest(MediaType: "video"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/feeds/rss_feed_items/any?limit=20&media_type=video");
    Assert.Equal("feed-1", response.RssFeedItems["item-1"].RssFeedId);
    Assert.Equal("video", response.RssFeedItems["item-1"].MediaContent?.Medium);
  }

  [Fact]
  public async Task FetchPodcastPlaybackPositionAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.podcast-playback-position.default");

    var response = await client.FetchPodcastPlaybackPositionAsync(
        "episode-1",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/podcast-episodes/episode-1/playback-position");
    Assert.Equal(45.5, response.PlaybackPosition?.PositionSeconds);
    Assert.False(response.PlaybackPosition?.IsCompleted);
  }

  [Fact]
  public async Task UpdatePodcastPlaybackPositionAsyncSendsBody()
  {
    var handler = new RecordingHandler("{}");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.UpdatePodcastPlaybackPositionAsync(
        "episode 1",
        12.5,
        completed: true,
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Put, "/api/v1/podcast-episodes/episode%201/playback-position");
    Assert.Equal("""{"position_seconds":12.5,"completed":true}""", handler.RequestBody);
  }

  [Fact]
  public async Task FetchPodcastEpisodeChaptersAsyncUsesResponseFixture()
  {
    var responseBody = """
        {
          "chapters": [
            {
              "start_seconds": 12.5,
              "end_seconds": null,
              "title": "Intro",
              "url": "https://example.com/chapters/intro",
              "image_url": "https://example.com/chapters/intro.png",
              "is_visible": true
            }
          ]
        }
        """;
    var handler = new RecordingHandler(responseBody);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchPodcastEpisodeChaptersAsync(
        "episode 1",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/podcast-episodes/episode%201/chapters");
    Assert.Single(response.Chapters);
    Assert.Equal(12.5, response.Chapters[0].StartSeconds);
    Assert.Null(response.Chapters[0].EndSeconds);
    Assert.Equal("Intro", response.Chapters[0].Title);
    Assert.Equal(new Uri("https://example.com/chapters/intro"), response.Chapters[0].Url);
    Assert.Equal(new Uri("https://example.com/chapters/intro.png"), response.Chapters[0].ImageUrl);
    Assert.True(response.Chapters[0].IsVisible);
  }

  [Fact]
  public void PodcastEpisodeChaptersDeserializeProductionObjects()
  {
    const string Json = """
        {
          "chapters": [
            {
              "start_seconds": 0,
              "end_seconds": 15,
              "title": "Intro",
              "is_visible": false
            }
          ]
        }
        """;

    var response = JsonSerializer.Deserialize<PodcastEpisodeChaptersResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal(0, response.Chapters[0].StartSeconds);
    Assert.Equal(15, response.Chapters[0].EndSeconds);
    Assert.Equal("Intro", response.Chapters[0].Title);
    Assert.False(response.Chapters[0].IsVisible);
  }

  [Fact]
  public async Task FetchRssFeedsAsyncUsesProductionStateFieldNames()
  {
    var (client, handler) = CreateClient("swift.rss-feeds.default");

    var response = await client.SendAsync<RssFeedsResponse>(
        VouchaApiEndpoints.RssFeedsForTopic("topic-1"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/rss-feeds?topic=topic-1");
    Assert.Equal("feed-1", response.Results[0].Id);
    Assert.True(response.Results[0].IsDiscoverable);
    Assert.Equal("topic-1", response.Results[0].Topic?.Id);
  }

  [Fact]
  public void RssFeedItemCategoriesDeserializeProductionCategoryObjects()
  {
    const string Json = """
        {
          "id": "item-1",
          "data": null,
          "published_at": "2026-01-01T00:00:00Z",
          "rss_feed_sources": [],
          "categories": [
            {
              "id": "category-1",
              "category_text": "Travel",
              "topic": {
                "id": "topic-1",
                "name": "Tech",
                "slug": "tech",
                "topic_type": "topic"
              },
              "votes_score_net": 3
            }
          ]
        }
        """;

    var item = JsonSerializer.Deserialize<RssFeedItem>(Json, VouchaApiJson.Options);

    Assert.NotNull(item);
    Assert.Equal("category-1", item.Categories?[0].Id);
    Assert.Equal("Travel", item.Categories?[0].CategoryText);
    Assert.Equal("topic-1", item.Categories?[0].Topic?.Id);
    Assert.Equal(3, item.Categories?[0].VotesScoreNet);
  }

  [Fact]
  public void RssFeedSourcePublisherTypeDeserializesTopicLikeObjects()
  {
    const string Json = """
        {
          "id": "feed-1",
          "title": "Example Feed",
          "feed_type": "article",
          "rss_feed_url": { "url": "https://example.com/feed.xml" },
          "home_page_url": { "url": "https://example.com" },
          "hostname": { "hostname": "example.com" },
          "publisher_type": {
            "id": "topic-1",
            "name": "Tech",
            "slug": "tech",
            "topic_type": "topic"
          }
        }
        """;

    var source = JsonSerializer.Deserialize<RssFeedSource>(Json, VouchaApiJson.Options);

    Assert.NotNull(source);
    Assert.Equal("topic-1", source.PublisherType?.Id);
    Assert.Equal("Tech", source.PublisherType?.Name);
    Assert.Equal("tech", source.PublisherType?.Slug);
  }

  [Fact]
  public async Task FetchPostsFeedAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.posts.feed.default");

    var response = await client.FetchPostsFeedAsync(
        new FetchPostsFeedRequest(),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/feeds/posts/any?limit=20&sort=hot&time_range=1w");
    Assert.Equal("p1", response.Results[0].EntityId);
    Assert.Equal("discussion", response.Posts["p1"].PostType);
    Assert.True(response.Posts.ContainsKey("p1"));
    Assert.True(response.Communities.ContainsKey("community-1"));
  }

  [Fact]
  public async Task FetchPostsFeedAsyncForwardsTheSelectedTimeRange()
  {
    var (client, handler) = CreateClient("swift.posts.feed.default");

    await client.FetchPostsFeedAsync(
        new FetchPostsFeedRequest(TimeRange: Voucha.Client.Core.Posts.PostTimeRanges.Last30Days),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/feeds/posts/any?limit=20&sort=hot&time_range=1m");
  }

  [Fact]
  public async Task FetchPostsAndPodcastRssRequestsForwardHackerNewsFilter()
  {
    var postHandler = new RecordingHandler("{}");
    var postClient = new VouchaApiClient(new HttpClient(postHandler) { BaseAddress = new Uri("https://api.test") });
    await postClient.FetchPostsAsync(
        new FetchPostsRequest(HasHackerNewsReferences: true),
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Get, postHandler.Method);
    ApiQueryAssertions.Equal("/api/v1/posts?sort=hot&limit=25&has_hacker_news_references=true&time_range=1m", postHandler.PathAndQuery);

    var rssHandler = new RecordingHandler("{}");
    var rssClient = new VouchaApiClient(new HttpClient(rssHandler) { BaseAddress = new Uri("https://api.test") });
    await rssClient.FetchAllRssFeedItemsAsync(
        mediaType: "audio",
        cancellationToken: TestContext.Current.CancellationToken,
        hasHackerNewsReferences: false);
    Assert.Equal(HttpMethod.Get, rssHandler.Method);
    ApiQueryAssertions.Equal("/api/v1/rss-feed-items?limit=20&media_type=audio&has_hacker_news_references=false", rssHandler.PathAndQuery);
  }

  [Fact]
  public async Task FetchPostThreadAsyncUsesSharedFixtures()
  {
    const string rootId = "70000000-0000-7000-8000-000000005400";
    const string commentA = "70000000-0000-7000-8000-000000005401";
    const string commentB = "70000000-0000-7000-8000-000000005402";
    const string commentC = "70000000-0000-7000-8000-000000005403";
    var (client, handler) = CreateClient(
        "native.comments.post-detail.default",
        "native.comments.descendants.default",
        "native.comments.ancestors.permalink");

    var detail = await client.FetchPostAsync("comment-root", TestContext.Current.CancellationToken);
    var descendants = await client.FetchPostDescendantsAsync(rootId, TestContext.Current.CancellationToken);
    var ancestors = await client.FetchPostAncestorsAsync(commentB, TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
    Assert.Equal($"/api/v1/posts/{commentB}/ancestors", handler.Requests[2].PathAndQuery);
    Assert.Equal(rootId, detail.Post.Id);
    Assert.Equal(2, detail.PostMetrics?.Count.Children);
    Assert.True(detail.Bookmarks?[detail.Post.Id]["save"]);
    Assert.Equal([commentA, commentB, commentC], descendants.Results.Select(x => x.Id));
    Assert.True(descendants.Posts[commentA].CanEditContent);
    Assert.Equal("commenter", descendants.Posts[commentB].CreatedBy?.Username);
    Assert.True(descendants.Posts[commentC].IsAnonymous);
    Assert.Equal(8, descendants.PostElections?[commentA].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Dislike, descendants.ElectionVotes?[commentB].Choice);
    Assert.Equal("<p>Top-level native comment</p>", descendants.MarkdownToHtml?[commentA]);
    Assert.True(descendants.Bookmarks?[commentA]["save"]);
    Assert.Equal(rootId, ancestors.Results[0].Id ?? ancestors.Results[0].EntityId);
  }

  [Fact]
  public async Task FetchRssFeedsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.rss-feeds.default");

    var response = await client.FetchRssFeedsAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/rss-feeds?limit=25");
    Assert.Equal("feed-1", response.Results[0].Id);
    Assert.Equal("Example Feed", response.Results[0].Title);
    Assert.Equal(NewsFeedSourceType.Article, response.Results[0].FeedType);
    Assert.Equal(new Uri("https://example.com/feed.xml"), response.Results[0].RssFeedUrl?.Url);
    Assert.Equal("example.com", response.Results[0].RssFeedUrl?.Hostname?.HostnameValue);
  }

  [Fact]
  public async Task CreateStoryPostFromStoryAsyncUsesTypedStoryResponse()
  {
    var handler = new RecordingHandler("""
        {
          "post": {
            "id": "post-1",
            "post_type": "story",
            "title": "Story title",
            "markdown": "",
            "created_by_id": "story-teller"
          }
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.CreateStoryPostFromStoryAsync(
        "story 1", "00000000-0000-4000-8000-000000000021", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/posts");
    Assert.Equal("00000000-0000-4000-8000-000000000021", handler.IdempotencyKey);
    Assert.Equal("post-1", response.Post.Id);
    Assert.Equal("story", response.Post.PostType);
  }

  [Fact]
  public async Task CreateLinkPostFromRssFeedItemAsyncUsesLinkPostResponse()
  {
    var handler = new RecordingHandler("""
        {
          "post": {
            "id": "post-1",
            "post_type": "link",
            "title": "Article title",
            "markdown": "",
            "created_by_id": "user-1"
          }
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.CreateLinkPostFromRssFeedItemAsync(
        "item 1", "00000000-0000-4000-8000-000000000022", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/posts");
    Assert.Equal("00000000-0000-4000-8000-000000000022", handler.IdempotencyKey);
    Assert.Equal("post-1", response.Post.Id);
    Assert.Equal("link", response.Post.PostType);
  }
}

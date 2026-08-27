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

    AssertRequest(handler, HttpMethod.Get, "/api/v1/feeds/posts/any?limit=20&sort=hot");
    Assert.Equal("p1", response.Results[0].EntityId);
    Assert.Equal("discussion", response.Posts["p1"].PostType);
    Assert.True(response.Posts.ContainsKey("p1"));
    Assert.True(response.Communities.ContainsKey("community-1"));
  }

  [Fact]
  public async Task FetchPostThreadAsyncUsesSharedFixtures()
  {
    var (client, handler) = CreateClient(
        "native.comments.post-detail.default",
        "native.comments.descendants.default",
        "native.comments.ancestors.permalink");

    var detail = await client.FetchPostAsync("comment-root", TestContext.Current.CancellationToken);
    var descendants = await client.FetchPostDescendantsAsync("comment-root-post", TestContext.Current.CancellationToken);
    var ancestors = await client.FetchPostAncestorsAsync("comment-b", TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
    Assert.Equal("/api/v1/posts/comment-b/ancestors", handler.Requests[2].PathAndQuery);
    Assert.Equal("comment-root-post", detail.Post.Id);
    Assert.Equal(2, detail.PostMetrics?.Count.Children);
    Assert.True(detail.Bookmarks?[detail.Post.Id]["save"]);
    Assert.Equal(["comment-a", "comment-b", "comment-c", "comment-d"], descendants.Results.Select(x => x.Id));
    Assert.True(descendants.Posts["comment-a"].CanEditContent);
    Assert.Equal("commenter", descendants.Posts["comment-b"].CreatedBy?.Username);
    Assert.True(descendants.Posts["comment-c"].IsAnonymous);
    Assert.NotNull(descendants.Posts["comment-d"].DeletedAt);
    Assert.Equal(2, descendants.PostsMetrics?["comment-b"].Count.Ancestors);
    Assert.Equal(8, descendants.PostElections?["comment-a"].VotesScoreNet);
    Assert.Equal(ElectionVoteChoice.Dislike, descendants.ElectionVotes?["comment-b"].Choice);
    Assert.Equal("<p>Top-level native comment</p>", descendants.MarkdownToHtml?["comment-a"]);
    Assert.True(descendants.Bookmarks?["comment-a"]["save"]);
    Assert.Equal("comment-root-post", ancestors.Results[0].Id ?? ancestors.Results[0].EntityId);
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
          },
          "story": {
            "id": "story-1",
            "title": "Story title",
            "cluster_reason": "clustered",
            "published_at": "2026-01-01T00:00:00Z",
            "official_rss_feed_item_id": "item-1",
            "official_locked_at": "2026-01-02T00:00:00Z",
            "created_at": "2026-01-01T00:00:00Z",
            "updated_at": "2026-01-03T00:00:00Z",
            "deleted_at": null
          },
          "postStory": {
            "post_id": "post-1",
            "story_id": "story-1",
            "initiated_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z"
          }
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.CreateStoryPostFromStoryAsync("story 1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/stories/story%201/discussions");
    Assert.Equal("post-1", response.Post.Id);
    Assert.Equal("story", response.Post.PostType);
    Assert.Equal("story-1", response.Story.Id);
    Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), response.Story.OfficialLockedAt);
    Assert.Equal(DateTimeOffset.Parse("2026-01-03T00:00:00Z"), response.Story.UpdatedAt);
    Assert.Equal("post-1", response.PostStory.PostId);
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

    var response = await client.CreateLinkPostFromRssFeedItemAsync("item 1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/rss-feed-items/item%201/discussions");
    Assert.Equal("post-1", response.Post.Id);
    Assert.Equal("link", response.Post.PostType);
  }
}

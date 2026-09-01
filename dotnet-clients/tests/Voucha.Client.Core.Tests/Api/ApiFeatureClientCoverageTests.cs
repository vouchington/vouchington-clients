using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.ReferralLinks;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiFeatureClientCoverageTests
{
  private const string PostsFeedJson = """
      {
        "results": [{ "__entity_type": "post", "id": "post-1", "key": "post-1" }],
        "page_info": { "has_next_page": false },
        "posts": {
          "post-1": {
            "id": "post-1",
            "post_type": "review",
            "title": "Review",
            "markdown": "Body",
            "created_by_id": "user-1"
          }
        },
        "users": {},
        "communities": {}
      }
      """;

  private const string PostJson = """
      {
        "post": {
          "id": "post-1",
          "post_type": "review",
          "title": "Review",
          "markdown": "Body",
          "created_by_id": "user-1"
        }
      }
      """;

  private const string TopicJson = """
      {
        "topic": {
          "id": "topic-1",
          "name": "Rewards",
          "slug": "rewards",
          "topic_type": "topic"
        },
        "topic_metrics": {
          "count": { "posts": 3 },
          "viewer_count": { "posts": 1 }
        }
      }
      """;

  [Fact]
  public async Task PostsServiceCoversClientFeatureMethods()
  {
    var (client, handler) = CreateClient(PostsFeedJson);
    var service = new ApiPostsService(client);

    var feed = await service.FetchPostsAsync(
        new FetchPostsRequest(Query: "reward", Limit: 5),
        TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/posts?limit=5&q=reward&sort=hot", handler.PathAndQuery);
    Assert.Equal("post-1", feed.Results[0].Id);

    await service.FetchFeedAsync(
        new FetchPostsFeedRequest("follow_users", Limit: 3, PostTypes: "review"),
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/feeds/posts/follow_users?limit=3&post_types=review&sort=hot", handler.PathAndQuery);

    handler.ResponseBody = PostJson;
    Assert.Equal("post-1", (await service.FetchPostAsync("post-1", TestContext.Current.CancellationToken)).Post.Id);
    Assert.Equal("/api/v1/posts/post-1", handler.PathAndQuery);

    var body = new CreatePostBody("review", "Review", "Body", "turnstile");
    Assert.Equal("post-1", (await service.CreatePostAsync(body, "00000000-0000-4000-8000-000000000051", TestContext.Current.CancellationToken)).Post.Id);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/posts", handler.PathAndQuery);
    Assert.Equal("00000000-0000-4000-8000-000000000051", handler.IdempotencyKey);

    Assert.Equal(
        "post-1",
        (await service.CreateCommunityPostAsync(
            "community-1",
            body,
            "00000000-0000-4000-8000-000000000052",
            TestContext.Current.CancellationToken)).Post.Id);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/communities/community-1/posts", handler.PathAndQuery);
    Assert.Equal("00000000-0000-4000-8000-000000000052", handler.IdempotencyKey);

    Assert.Equal(
        "post-1",
        (await service.UpdatePostAsync(
            "post-1",
            new UpdatePostBody(Title: "Updated"),
            TestContext.Current.CancellationToken)).Post.Id);
    Assert.Equal(HttpMethod.Patch, handler.Method);

    await service.ArchivePostAsync("post-1", TestContext.Current.CancellationToken);
    Assert.Contains("\"archive\":true", handler.RequestBody!, StringComparison.Ordinal);

    await service.UnarchivePostAsync("post-1", TestContext.Current.CancellationToken);
    Assert.Contains("\"archive\":false", handler.RequestBody!, StringComparison.Ordinal);

    handler.ResponseBody = "{}";
    await service.DeletePostAsync("post-1", TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Delete, handler.Method);
  }

  [Fact]
  public async Task ContributionStatusDecodesTheRequestedActionLimit()
  {
    var (client, handler) = CreateClient("""
        {"admission":{"allowed":true},"contribution_status":{"allowed":true},"daily_quota":{"limit":10,"used":2},"action_limit":{"action":"story_discussion","allowed":false,"daily_window":{"limit":3,"used":3,"window_seconds":86400},"short_window":{"limit":1,"used":1,"window_seconds":60},"tier":"new"}}
        """);

    var response = await client.FetchContributionStatusAsync(
        "story_discussion", TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/my/contribution-status?action=story_discussion", handler.PathAndQuery);
    Assert.Equal("story_discussion", response.ActionLimit?.Action);
    Assert.False(response.ActionLimit?.Allowed ?? true);
    Assert.Equal(3, response.ActionLimit?.DailyWindow.Used);
    Assert.Equal(60, response.ActionLimit?.ShortWindow.WindowSeconds);
  }

  [Fact]
  public async Task ClientFeatureMethodsCoverRelationsAndImageUpload()
  {
    var (client, handler) = CreateClient("""
        {
          "results": [],
          "page_info": { "has_next_page": false },
          "entity_relations": {}
        }
        """);

    await client.FetchEntityRelationsAsync(
        new EntityRelationsRequest("post", "post-1", "related", "url", Limit: 3),
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/entity-relations/post/post-1/related/url?limit=3&sort=best", handler.PathAndQuery);

    handler.ResponseBody = """
        {
          "relation": { "id": "relation-1", "subject_id": "post-1", "object_id": "url-1" }
        }
        """;
    var relation = await client.CreateEntityRelationAsync(
        new CreateEntityRelationRequest("post", "post-1", "related", "url", "url-1"),
        TestContext.Current.CancellationToken);
    Assert.Equal("relation-1", relation.Relation.Id);
    Assert.Contains("\"objectId\":\"url-1\"", handler.RequestBody!, StringComparison.Ordinal);

    handler.ResponseBody = """
        {
          "upload": {
            "image_id": "image-1",
            "upload_url": "https://s3.example.test/upload",
            "content_type": "image/png",
            "expires_at": "2026-01-01T00:00:00Z"
          }
        }
        """;
    var upload = await client.CreateImageUploadUrlAsync(
        new CreateImageUploadUrlBody("image/png", 123),
        TestContext.Current.CancellationToken);
    Assert.Equal("image-1", upload.Upload.ImageId);

    handler.ResponseBody = """
        { "image": { "id": "image-1", "upload_status": "processing" } }
        """;
    Assert.Equal(
        "processing",
        (await client.CompleteImageUploadAsync(
            "image-1",
            TestContext.Current.CancellationToken)).Image.UploadStatus);

    handler.ResponseBody = """
        {
          "upload_state": {
            "id": "image-1",
            "upload_status": "complete",
            "upload_error": null,
            "ready": true,
            "blocked": false
          }
        }
        """;
    Assert.True((await client.FetchImageUploadStateAsync(
        "image-1",
        TestContext.Current.CancellationToken)).UploadState.Ready);
  }

  [Fact]
  public async Task FollowerDistributionClientMethodsUseTypedResponsesAndBodylessShares()
  {
    var (client, handler) = CreateClient("""
        { "status": "accepted", "distribution_id": "distribution-1" }
        """);

    var postShare = await client.SharePostWithFollowersAsync("post-1", TestContext.Current.CancellationToken);
    Assert.Equal("accepted", postShare.Status);
    Assert.Null(handler.RequestBody);
    Assert.Equal("/api/v1/posts/post-1/shares", handler.PathAndQuery);

    await client.SendPostToFollowersAsync(
        "post-1", FollowerDistributionBody.Selected(["00000000-0000-7000-8000-000000000001"]), TestContext.Current.CancellationToken);
    Assert.Contains("recipient_user_ids", handler.RequestBody!, StringComparison.Ordinal);

    await client.ShareRssFeedItemWithFollowersAsync("item-1", TestContext.Current.CancellationToken);
    Assert.Null(handler.RequestBody);
    Assert.Equal("/api/v1/rss-feed-items/item-1/shares", handler.PathAndQuery);

    await client.SendRssFeedItemToFollowersAsync(
        "item-1", FollowerDistributionBody.AllFollowers(), TestContext.Current.CancellationToken);
    Assert.Contains("all_followers", handler.RequestBody!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task TopicServiceCoversDetailAndMutationMethods()
  {
    var (client, handler) = CreateClient(TopicJson);
    var service = new ApiTopicsService(client);

    var topic = await service.FetchTopicAsync("rewards", TestContext.Current.CancellationToken);
    Assert.Equal("Rewards", topic.Topic.Name);
    Assert.Equal(3, topic.TopicMetrics?.Count?["posts"]);
    Assert.Equal(1, topic.TopicMetrics?.ViewerCount?["posts"]);
    Assert.Equal("/api/v1/topics/rewards", handler.PathAndQuery);

    handler.ResponseBody = """
        {
          "results": [
            {
              "id": "feed-1",
              "title": "Rewards News",
              "feed_type": "article",
              "rss_feed_url": { "url": "https://example.com/feed.xml" },
              "home_page_url": { "url": "https://example.com" },
              "hostname": { "hostname": "example.com" },
              "topic": {
                "id": "topic-1",
                "name": "Rewards",
                "slug": "rewards",
                "topic_type": "rss_feed"
              },
              "last_fetched_at": null,
              "is_enabled": true,
              "is_discoverable": true
            }
          ],
          "page_info": { "has_next_page": false },
          "bookmarks": {
            "feed-1": { "follow": true }
          }
        }
        """;
    var rssFeeds = await service.FetchRssFeedsForTopicAsync(
        "topic-1",
        cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/rss-feeds?topic=topic-1", handler.PathAndQuery);
    Assert.True(rssFeeds.Bookmarks?["feed-1"].Follow);

    handler.ResponseBody = """
        {
          "topic": {
            "id": "topic-2",
            "name": "Cards",
            "slug": "cards",
            "topic_type": "topic"
          }
        }
        """;
    Assert.Equal(
        "topic-2",
        (await service.CreateTopicAsync(
            new CreateTopicRequest("Cards", "cards", "topic"),
            TestContext.Current.CancellationToken)).Topic.Id);
    Assert.Equal(HttpMethod.Post, handler.Method);

    Assert.Equal(
        "topic-2",
        (await service.UpdateTopicAsync(
            "cards",
            new UpdateTopicBody(Name: "Cards"),
            TestContext.Current.CancellationToken)).Topic.Id);
    Assert.Equal(HttpMethod.Patch, handler.Method);
    Assert.Equal("/api/v1/topics/cards", handler.PathAndQuery);

    await service.FollowTopicAsync("topic-2", TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Put, handler.Method);
    Assert.Equal("/api/v1/bookmarks/topic/topic-2/follow", handler.PathAndQuery);

    await service.UnfollowTopicAsync("topic-2", TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Delete, handler.Method);
    Assert.Equal("/api/v1/bookmarks/topic/topic-2/follow", handler.PathAndQuery);
  }

  [Fact]
  public async Task ReferralLinkServiceCoversFeedAndMutations()
  {
    var (client, handler) = CreateClient("""
        {
          "results": [],
          "users": {},
          "page_info": { "has_next_page": false }
        }
        """);
    var service = new ApiReferralLinksService(client);

    await service.FetchFeedAsync(
        new FetchReferralLinksFeedRequest("mutual_follows", "cursor", 7),
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/feeds/referral_links/mutual_follows?after=cursor&limit=7", handler.PathAndQuery);

    handler.ResponseBody = """
        {
          "results": [],
          "page_info": { "has_next_page": false }
        }
        """;
    await service.FetchMineAsync(new FetchReferralLinksRequest("cursor", 8), TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/referral-links?after=cursor&limit=8", handler.PathAndQuery);

    handler.ResponseBody = """
        {
          "results": [],
          "users": {}
        }
        """;
    await service.FetchPrioritizedAsync(
        new FetchPrioritizedReferralLinksRequest("program-1", true),
        TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/topics/program-1/prioritized-referral-links?all=true", handler.PathAndQuery);

    handler.ResponseBody = "{}";
    await service.CreateAsync(
        new CreateReferralLinkBody("program-1", new Uri("https://example.com/ref"), "Referral"),
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/referral-links", handler.PathAndQuery);

    await service.UpdateAsync("link-1", new UpdateReferralLinkBody(null), TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Patch, handler.Method);
    Assert.Contains("\"label\":null", handler.RequestBody!, StringComparison.Ordinal);

    await service.ActivateAsync("link-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/referral-links/link-1/activations", handler.PathAndQuery);

    await service.DeactivateAsync("link-1", TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/referral-links/link-1/activations", handler.PathAndQuery);

    await service.DeleteAsync("link-1", TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Delete, handler.Method);
  }

  [Fact]
  public async Task ClientFeatureEndpointsDeserializeDtoResponses()
  {
    var (client, handler) = CreateClient("""
        {
          "validation_info": {
            "user_help_text": "Paste an application URL.",
            "example_urls": ["https://example.com/apply"]
          }
        }
        """);

    var validation = await client.FetchReferralProgramValidationInfoAsync(
        "program-1",
        TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/topics/program-1/referral-program/validation-info", handler.PathAndQuery);
    Assert.Equal("Paste an application URL.", validation.ValidationInfo.UserHelpText);
    Assert.Equal("https://example.com/apply", validation.ValidationInfo.ExampleUrls[0]);

    var report = new ReportBody("post", "post-1", "spam", "details", "turnstile");
    var distribution = FollowerDistributionBody.Selected(["00000000-0000-7000-8000-000000000001"]);
    var accepted = new FollowerDistributionAcceptedResponse("accepted", "distribution-1");

    var json = JsonSerializer.Serialize(new { report, distribution, accepted }, VouchaApiJson.Options);
    Assert.Contains("entityType", json, StringComparison.Ordinal);
    Assert.Contains("entityId", json, StringComparison.Ordinal);
    Assert.Contains("note", json, StringComparison.Ordinal);
    Assert.Contains("cf_turnstile_response", json, StringComparison.Ordinal);
    Assert.Contains("recipient_user_ids", json, StringComparison.Ordinal);
    Assert.Contains("distribution_id", json, StringComparison.Ordinal);

    handler.ResponseBody = "{}";
    await client.SendAsync(
        VouchaApiEndpoints.Report(report),
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/reports", handler.PathAndQuery);
    Assert.Contains("entityType", handler.RequestBody!, StringComparison.Ordinal);
  }

  private static (VouchaApiClient Client, MutableRecordingHandler Handler) CreateClient(string responseBody)
  {
    var handler = new MutableRecordingHandler(responseBody);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (client, handler);
  }

  private sealed class MutableRecordingHandler : HttpMessageHandler
  {
    public MutableRecordingHandler(string responseBody) => ResponseBody = responseBody;

    public HttpMethod? Method { get; private set; }

    public string? PathAndQuery { get; private set; }

    public string? RequestBody { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public string ResponseBody { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Method = request.Method;
      PathAndQuery = request.RequestUri?.PathAndQuery;
      IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
      RequestBody = request.Content is null
          ? null
          : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(ResponseBody),
        RequestMessage = request,
      };
    }
  }
}

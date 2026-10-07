using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class ApiTopicsServiceTests
{
  [Fact]
  public async Task ServiceMethodsUseTopicApiEndpoints()
  {
    var handler = new QueueHandler(
        Json(TopicSearchJson()),
        Json(TopicResponseJson()),
        Json(TopicMutationJson("created")),
        Json(TopicMutationJson("updated")),
        Json(RssFeedsJson()),
        Json("{}"),
        Json("{}"),
        Json("{}"),
        Json("{}"),
        Json("{}"),
        Json("""{"results":[{"id":"alias-1","alias":"alt","topic_id":"topic-1"}],"page_info":{"has_next_page":false}}"""),
        Json(
            """
            {"results":[{"hostname_id":"host-1","hostname":"example.com","topic_id":"topic-1"}],
            "page_info":{"has_next_page":false}}
            """),
        Json("{}"),
        Json(RssFeedsJson()),
        Json("{}"),
        Json("""{"additional_hostname":{"hostname_id":"host-2","hostname":"extra.example.com","topic_id":"topic-1"}}"""),
        Json("{}"),
        Json(TopicMergeJson()));
    var service = new ApiTopicsService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await service.SearchAsync("topic", TestContext.Current.CancellationToken);
    await service.FetchTopicAsync("topic 1", TestContext.Current.CancellationToken);
    await service.CreateTopicAsync(new CreateTopicRequest("Created", "created", "topic"), TestContext.Current.CancellationToken);
    await service.UpdateTopicAsync("topic 1", new UpdateTopicBody(Name: "Updated"), TestContext.Current.CancellationToken);
    await service.FetchRssFeedsForTopicAsync("topic 1", cancellationToken: TestContext.Current.CancellationToken);
    await service.FollowTopicAsync("topic 1", TestContext.Current.CancellationToken);
    await service.UnfollowTopicAsync("topic 1", TestContext.Current.CancellationToken);
    await service.FollowSourceAsync("feed 1", TestContext.Current.CancellationToken);
    await service.UnfollowSourceAsync("feed 1", TestContext.Current.CancellationToken);
    await service.UpdateSourceAsync(
        "feed 1",
        new UpdateRssFeedBody(Enabled: false),
        TestContext.Current.CancellationToken);
    await service.FetchTopicAliasesAsync("topic 1", cancellationToken: TestContext.Current.CancellationToken);
    await service.FetchTopicAdditionalHostnamesAsync("topic 1", cancellationToken: TestContext.Current.CancellationToken);
    await service.CreateTopicAliasesAsync("topic 1", new CreateTopicAliasesBody("alt"), TestContext.Current.CancellationToken);
    await service.FetchRssFeedsForTopicAsync("topic 1", RssFeedEnabledFilter.All, TestContext.Current.CancellationToken);
    await service.DeleteTopicAliasAsync("topic 1", "00000000-0000-7000-8000-000000000001", TestContext.Current.CancellationToken);
    await service.CreateTopicAdditionalHostnameAsync("topic 1", "extra.example.com", TestContext.Current.CancellationToken);
    await service.DeleteTopicAdditionalHostnameAsync("topic 1", "host 1", TestContext.Current.CancellationToken);
    await service.MergeTopicAliasesAsync("topic 1", "topic 2", TestContext.Current.CancellationToken);

    Assert.Equal(
        [
          (HttpMethod.Get, "/api/v1/topics?q=topic", null),
          (HttpMethod.Get, "/api/v1/topics/topic%201", null),
          (HttpMethod.Post, "/api/v1/topics", """{"name":"Created","slug":"created","topic_type":"topic"}"""),
          (HttpMethod.Patch, "/api/v1/topics/topic%201", """{"name":"Updated"}"""),
          (HttpMethod.Get, "/api/v1/rss-feeds?topic=topic%201", null),
          (HttpMethod.Put, "/api/v1/bookmarks/topic/topic%201/follow", null),
          (HttpMethod.Delete, "/api/v1/bookmarks/topic/topic%201/follow", null),
          (HttpMethod.Put, "/api/v1/bookmarks/rss_feed/feed%201/follow", null),
          (HttpMethod.Delete, "/api/v1/bookmarks/rss_feed/feed%201/follow", null),
          (HttpMethod.Patch, "/api/v1/rss-feeds/feed%201", """{"enabled":false}"""),
          (HttpMethod.Get, "/api/v1/topics/topic%201/aliases", null),
          (HttpMethod.Get, "/api/v1/topics/topic%201/additional-hostnames", null),
          (HttpMethod.Post, "/api/v1/topics/topic%201/aliases", """{"aliases":"alt"}"""),
          (HttpMethod.Get, "/api/v1/rss-feeds?enabled=null&topic=topic%201", null),
          (HttpMethod.Delete, "/api/v1/topics/topic%201/aliases/00000000-0000-7000-8000-000000000001", null),
          (HttpMethod.Post, "/api/v1/topics/topic%201/additional-hostnames", """{"hostname":"extra.example.com"}"""),
          (HttpMethod.Delete, "/api/v1/topics/topic%201/additional-hostnames/host%201", null),
          (HttpMethod.Post, "/api/v1/topics/topic%201/merges", """{"destination_id_or_slug":"topic 2"}"""),
        ],
        handler.Requests);
  }

  [Fact]
  public async Task UpdateTopicUsesCurrentPolicyFlagNames()
  {
    var handler = new QueueHandler(Json(TopicMutationJson("Updated")));
    var service = new ApiTopicsService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var response = await service.UpdateTopicAsync("topic-1", new UpdateTopicBody(Noindex: true, AllowReviews: false), TestContext.Current.CancellationToken);

    Assert.Equal(
        (HttpMethod.Patch, "/api/v1/topics/topic-1", """{"is_noindexed":true,"should_allow_reviews":false}"""),
        Assert.Single(handler.Requests));
    Assert.False(response.Topic.Noindex);
    Assert.True(response.Topic.AllowReviews);
  }

  [Fact]
  public async Task FetchMethodsForwardAfterAndLimitToTheQueryString()
  {
    var handler = new QueueHandler(
        Json("""{"results":[{"id":"alias-1","alias":"alt","topic_id":"topic-1"}],"page_info":{"has_next_page":true,"end_cursor":"cursor-1"}}"""),
        Json(
            """
            {"results":[{"hostname_id":"host-1","hostname":"example.com","topic_id":"topic-1"}],
            "page_info":{"has_next_page":true,"end_cursor":"cursor-2"}}
            """));
    var service = new ApiTopicsService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var aliases = await service.FetchTopicAliasesAsync(
        "topic 1",
        after: "cursor-0",
        limit: 5,
        cancellationToken: TestContext.Current.CancellationToken);
    var hostnames = await service.FetchTopicAdditionalHostnamesAsync(
        "topic 1",
        after: "cursor-1",
        limit: 5,
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(
        [
          (HttpMethod.Get, "/api/v1/topics/topic%201/aliases?after=cursor-0&limit=5", null),
          (HttpMethod.Get, "/api/v1/topics/topic%201/additional-hostnames?after=cursor-1&limit=5", null),
        ],
        handler.Requests);
    Assert.True(aliases.PageInfo.HasNextPage);
    Assert.Equal("cursor-1", aliases.PageInfo.EndCursor);
    Assert.True(hostnames.PageInfo.HasNextPage);
    Assert.Equal("cursor-2", hostnames.PageInfo.EndCursor);
  }

  private static (string Body, HttpStatusCode StatusCode) Json(string body) => (body, HttpStatusCode.OK);

  private static string TopicSearchJson() =>
      """
      {"results":[],"page_info":{"has_next_page":false},"topics":{},"topics_metrics":{}}
      """;

  private static string TopicResponseJson() =>
      $$"""
      {
        "topic": {{TopicJson("topic-1", "Topic")}},
        "topic_election": null,
        "election_vote": null,
        "bookmarks": null
      }
      """;

  private static string TopicMutationJson(string name) =>
      $$"""{"topic":{{TopicJson("topic-1", name)}}}""";

  private static string TopicMergeJson() =>
      $$"""
      {
        "topic": {{TopicJson("topic-2", "Destination")}},
        "topic_merge": {
          "source_topic_id": "topic-1",
          "destination_topic_id": "topic-2",
          "moved_aliases": ["alt"]
        }
      }
      """;

  private static string RssFeedsJson() =>
      $$"""
      {
        "results": [
          {
            "id": "feed-1",
            "title": "Feed",
            "feed_type": "article",
            "rss_feed_url": {"url":"https://example.com/feed.xml"},
            "home_page_url": null,
            "hostname": {"hostname":"example.com"},
            "topic": {{TopicJson("topic-1", "Topic")}}
          }
        ],
        "page_info": {"has_next_page": false},
        "bookmarks": null
      }
      """;

  private static string TopicJson(string id, string name) =>
      $$"""
      {
        "id": "{{id}}",
        "name": "{{name}}",
        "slug": "{{id}}",
        "topic_type": "rss_feed",
        "markdown": "Body",
        "aliases": ["alt"],
        "should_allow_reviews": true,
        "created_at": "2026-01-01T00:00:00Z",
        "hero_image_id": "hero-1",
        "hostname": {"id":"hostname-1","hostname":"example.com","topic_id":"{{id}}"},
        "hostname_id": "hostname-1",
        "logo_image_id": "logo-1",
        "is_noindexed": false
      }
      """;

  private sealed class QueueHandler : HttpMessageHandler
  {
    private readonly Queue<(string Body, HttpStatusCode StatusCode)> responses;

    public QueueHandler(params (string Body, HttpStatusCode StatusCode)[] responses) =>
        this.responses = new Queue<(string Body, HttpStatusCode StatusCode)>(responses);

    public List<(HttpMethod Method, string? PathAndQuery, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var body = request.Content is null
          ? null
          : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      Requests.Add((request.Method, request.RequestUri?.PathAndQuery, body));
      var (responseBody, statusCode) = responses.Dequeue();
      return new HttpResponseMessage(statusCode)
      {
        Content = new StringContent(responseBody),
        RequestMessage = request,
      };
    }
  }
}

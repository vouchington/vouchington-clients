using System.Net;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tags;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Tags;

public sealed partial class TagManagementViewModelTests
{
  [Fact]
  public async Task LoadAsyncWithoutContextLeavesTheViewModelEmpty()
  {
    var client = new VouchaApiClient(new HttpClient(new RecordingHandler("{}"))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    var viewModel = new TagManagementViewModel(client);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasContext);
    Assert.Empty(viewModel.Tabs);
    Assert.Null(viewModel.SelectedTab);
    Assert.Empty(viewModel.Relations);
    Assert.Empty(viewModel.SearchResults);
    Assert.Empty(viewModel.PublisherTypes);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task LoadAsyncUsesFallbackLabelForUnknownEntityTypes()
  {
    var client = new VouchaApiClient(new HttpClient(new RecordingHandler("{}"))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("unknown", "mystery", "topic"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("mystery", viewModel.EntityLabel);
    Assert.Equal("topic", viewModel.Title);
    Assert.Empty(viewModel.Tabs);
    Assert.Null(viewModel.SelectedTab);
    Assert.Empty(viewModel.Relations);
  }

  [Fact]
  public void SetContextUsesTheSupportedTabSets()
  {
    var client = new VouchaApiClient(new HttpClient(new RecordingHandler("{}"))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "post"));

    Assert.Equal(3, viewModel.Tabs.Count);
    Assert.Equal("post", viewModel.SelectedTab?.Value);

    viewModel.SetContext(new TagManagementRouteContext("topic", "topic-1", "publisher_type", "topic"));

    Assert.DoesNotContain(viewModel.Tabs, tab => tab.Value == "publisher_type");
    Assert.Null(viewModel.SelectedTab);

    viewModel.SetContext(new TagManagementRouteContext("topic", "topic-1", "publisher_type", "rss_feed"));

    Assert.Contains(viewModel.Tabs, tab => tab.Value == "publisher_type");
    Assert.Equal("publisher_type", viewModel.SelectedTab?.Value);

    viewModel.SetContext(new TagManagementRouteContext("rss_feed_item", "item-1", "topic"));

    Assert.Single(viewModel.Tabs);
    Assert.Equal("topic", viewModel.SelectedTab?.Value);

    viewModel.SetContext(new TagManagementRouteContext("custom", "value", "topic"));

    Assert.Empty(viewModel.Tabs);
    Assert.Null(viewModel.SelectedTab);
  }

  [Fact]
  public async Task LoadAsyncCoversEntityLoadsSearchesAndTabFiltering()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(PostResponseJson()),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(SearchPostsJson()),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(SearchTopicsJson()),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(SearchUrlsJson()),
        new RecordedResponse(TopicResponseJson("topic")),
        new RecordedResponse(TopicResponseJson("rss_feed")),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(PublisherTypesJson()),
        new RecordedResponse(RssFeedItemResponseJson()),
        new RecordedResponse(EntityRelationsJson()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "post"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Review Post", viewModel.EntityLabel);
    Assert.Equal("Related Posts", viewModel.Title);
    Assert.Single(viewModel.Relations);
    Assert.Equal(new TagRelationRow("relation-1", "Related Topic", "topic-2", 4.5), viewModel.Relations[0]);

    viewModel.SearchQuery = "re";
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.SearchResults);
    Assert.Equal(new TagSearchResultRow("post-2", "Suggested Post", "discussion"), viewModel.SearchResults[0]);

    await viewModel.SelectTabAsync("topic", TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.SearchQuery);
    Assert.Empty(viewModel.SearchResults);
    viewModel.SearchQuery = "top";
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.SearchResults);
    Assert.Equal(new TagSearchResultRow("topic-2", "Suggested Topic", "suggested-topic"), viewModel.SearchResults[0]);

    await viewModel.SelectTabAsync("missing", TestContext.Current.CancellationToken);
    Assert.Equal(5, handler.Requests.Count);

    await viewModel.SelectTabAsync("url", TestContext.Current.CancellationToken);
    viewModel.SearchQuery = "   ";
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.SearchResults);

    var requestCountBeforeShortUrlSearch = handler.Requests.Count;
    viewModel.SearchQuery = "vo";
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.SearchResults);
    Assert.Equal(requestCountBeforeShortUrlSearch, handler.Requests.Count);

    viewModel.SearchQuery = "voucha";
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.SearchResults);
    Assert.Equal(new TagSearchResultRow("url-2", "https://voucha.example/rewards", "example.com"), viewModel.SearchResults[0]);

    viewModel.SetContext(new TagManagementRouteContext("topic", "topic-1", "publisher_type", "topic"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.DoesNotContain(viewModel.Tabs, tab => tab.Value == "publisher_type");
    Assert.Equal("Publisher Type", viewModel.EntityLabel);
    Assert.Equal("Tags", viewModel.Title);
    Assert.Null(viewModel.SelectedTab);
    Assert.Empty(viewModel.Relations);

    viewModel.SetContext(new TagManagementRouteContext("topic", "topic-1", "publisher_type", "rss_feed"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Publisher Type", viewModel.EntityLabel);
    Assert.Equal("publisher_type", viewModel.SelectedTab?.Value);
    Assert.True(viewModel.HasPublisherTypes);
    Assert.Single(viewModel.PublisherTypes);

    viewModel.SearchQuery = "pub";
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);
    Assert.Single(viewModel.SearchResults);
    Assert.Equal(new TagSearchResultRow("publisher-type-1", "Publisher Type", "publisher-type"), viewModel.SearchResults[0]);

    viewModel.SetContext(new TagManagementRouteContext("rss_feed_item", "item-1", "topic"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Item Title", viewModel.EntityLabel);
    Assert.Equal("Categories", viewModel.Title);
    Assert.Single(viewModel.Relations);
    Assert.Equal(13, handler.Requests.Count);

    Assert.Equal(
        [
          "/api/v1/posts/post-1",
          "/api/v1/entity-relations/post/post-1/related/post?limit=25&positiveNetVoteScore=false&sort=best",
          "/api/v1/posts?limit=10&q=re",
          "/api/v1/entity-relations/post/post-1/category/topic?limit=25&positiveNetVoteScore=false&sort=best",
          "/api/v1/topics?q=top",
          "/api/v1/entity-relations/post/post-1/related/url?limit=25&positiveNetVoteScore=false&sort=best",
          "/api/v1/urls?limit=10&query=voucha",
          "/api/v1/topics/topic-1",
          "/api/v1/topics/topic-1",
          "/api/v1/entity-relations/topic/topic-1/publisher_type/topic?limit=25&positiveNetVoteScore=false&sort=best",
          "/api/v1/topics/publisher-types",
          "/api/v1/rss-feed-items/item-1",
          "/api/v1/entity-relations/rss_feed_item/item-1/category/topic?limit=25&positiveNetVoteScore=false&sort=best",
        ],
        handler.Requests.Select(request => request.PathAndQuery));
  }

  [Fact]
  public async Task AddTagAndVoteRelationUseTheEntityRelationEndpoints()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(TopicResponseJson("rss_feed")),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(PublisherTypesJson()),
        new RecordedResponse(CreateRelationJson()),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(PublisherTypesJson()),
        new RecordedResponse("{}"),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(PublisherTypesJson()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("topic", "topic-1", "publisher_type", "rss_feed"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.AddTagAsync("   ", TestContext.Current.CancellationToken);
    await viewModel.VoteRelationAsync("   ", ElectionVoteChoice.Confirm, TestContext.Current.CancellationToken);
    await viewModel.SelectTabAsync("missing", TestContext.Current.CancellationToken);

    await viewModel.AddTagAsync("topic-2", TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.SearchResults);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(6, handler.Requests.Count);
    Assert.Equal(
        "/api/v1/entity-relations/topic/topic-1/publisher_type/topic",
        handler.Requests[3].PathAndQuery);
    Assert.Contains("\"objectId\":\"topic-2\"", handler.Requests[3].Body!, StringComparison.Ordinal);

    await viewModel.VoteRelationAsync("relation-1", ElectionVoteChoice.Dispute, TestContext.Current.CancellationToken);

    Assert.Equal(9, handler.Requests.Count);
    Assert.Equal("/api/v1/entity-relations/relation-1/vote", handler.Requests[6].PathAndQuery);
  }

  [Fact]
  public async Task SelectTabAsyncIgnoresConcurrentRequests()
  {
    var handler = new DeferredQueueHandler([EntityRelationsJson()]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "post"));

    var first = viewModel.SelectTabAsync("topic", TestContext.Current.CancellationToken);
    await handler.Started.Task;

    var second = viewModel.SelectTabAsync("url", TestContext.Current.CancellationToken);
    await second;

    Assert.Single(handler.Requests);
    handler.Release.TrySetResult();

    await first;

    Assert.Equal("topic", viewModel.SelectedTab?.Value);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task AddTagAsyncIgnoresConcurrentRequests()
  {
    var handler = new DeferredQueueHandler([CreateRelationJson(), EntityRelationsJson()]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));

    var first = viewModel.AddTagAsync("topic-2", TestContext.Current.CancellationToken);
    await handler.Started.Task;

    var second = viewModel.AddTagAsync("topic-3", TestContext.Current.CancellationToken);
    await second;

    handler.Release.TrySetResult();
    await first;

    Assert.Equal(2, handler.Requests.Count);
    Assert.Contains("\"objectId\":\"topic-2\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/entity-relations/post/post-1/category/topic?limit=25&positiveNetVoteScore=false&sort=best", handler.Requests[1].PathAndQuery);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task VoteRelationAsyncIgnoresConcurrentRequests()
  {
    var handler = new DeferredQueueHandler(["{}", EntityRelationsJson()]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);

    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));

    var first = viewModel.VoteRelationAsync("relation-1", ElectionVoteChoice.Confirm, TestContext.Current.CancellationToken);
    await handler.Started.Task;

    var second = viewModel.VoteRelationAsync("relation-2", ElectionVoteChoice.Dispute, TestContext.Current.CancellationToken);
    await second;

    handler.Release.TrySetResult();
    await first;

    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal("/api/v1/entity-relations/relation-1/vote", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/entity-relations/post/post-1/category/topic?limit=25&positiveNetVoteScore=false&sort=best", handler.Requests[1].PathAndQuery);
    Assert.False(viewModel.IsLoading);
  }

  private static string PostResponseJson() =>
      """
      {
        "post": {
          "id": "post-1",
          "post_type": "review",
          "title": "Review Post",
          "markdown": "Body",
          "created_by_id": "user-1"
        }
      }
      """;

  private static string TopicResponseJson(string topicType) =>
      $$"""
      {
        "topic": {
          "id": "topic-1",
          "name": "Publisher Type",
          "slug": "publisher-type",
          "topic_type": "{{topicType}}"
        }
      }
      """;

  private static string RssFeedItemResponseJson() =>
      """
      {
        "rss_feed_item": {
          "id": "item-1",
          "data": { "title": "Item Title" },
          "rss_feed": {
            "id": "feed-1",
            "title": "Feed",
            "feed_type": "article",
            "rss_feed_url": { "url": "https://example.com/feed.xml" },
            "home_page_url": null,
            "hostname": { "hostname": "example.com" }
          },
          "media_type": "article",
          "published_at": "2026-01-01T00:00:00Z",
          "rss_feed_sources": [],
          "title": null,
          "link": "https://example.com/item-1",
          "rss_feed_id": "feed-1"
        }
      }
      """;

  private static string EntityRelationsJson() =>
      """
      {
        "results": [
          { "__entity_type": "entity_relation", "id": "relation-1" }
        ],
        "page_info": { "has_next_page": false },
        "entity_relations": {
          "relation-1": {
            "id": "relation-1",
            "object_id": "topic-2",
            "object_data": "Related Topic",
            "votes_score_net": 4.5
          }
        }
      }
      """;


  private sealed class DeferredQueueHandler : HttpMessageHandler
  {
    private readonly Queue<string> responses;

    public DeferredQueueHandler(IEnumerable<string> responses)
    {
      this.responses = new Queue<string>(responses);
    }

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      Requests.Add(new RecordedRequest(request.Method, request.RequestUri?.PathAndQuery, body));
      Started.TrySetResult();
      await Release.Task.ConfigureAwait(false);

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(
            responses.Count > 0 ? responses.Dequeue() : "{}",
            Encoding.UTF8,
            "application/json"),
        RequestMessage = request,
      };
    }
  }

  private static string SearchPostsJson() =>
      """
      {
        "results": [
          { "__entity_type": "post", "id": "post-2", "slug": "suggested-post" }
        ],
        "page_info": { "has_next_page": false },
        "posts": {
          "post-2": {
            "id": "post-2",
            "post_type": "discussion",
            "title": "Suggested Post",
            "slug": "suggested-post",
            "markdown": "Body",
            "created_by_id": "user-1"
          }
        },
        "users": {},
        "communities": {}
      }
      """;

  private static string SearchTopicsJson() =>
      """
      {
        "results": [
          { "__entity_type": "topic", "id": "topic-2", "slug": "suggested-topic" }
        ],
        "page_info": { "has_next_page": false },
        "topics": {
          "topic-2": {
            "id": "topic-2",
            "name": "Suggested Topic",
            "slug": "suggested-topic",
            "topic_type": "topic"
          }
        },
        "topics_metrics": {}
      }
      """;

  private static string SearchUrlsJson() =>
      """
      {
        "results": [
          {
            "__entity_type": "url",
            "id": "url-2",
            "hostname": { "hostname": "example.com" },
            "url": "https://voucha.example/rewards",
            "pathname": "/rewards"
          }
        ],
        "page_info": { "has_next_page": false }
      }
      """;

  private static string PublisherTypesJson() =>
      """
      {
        "publisher_types": [
          {
            "id": "publisher-type-1",
            "label": "Publisher Type",
            "slug": "publisher-type"
          }
        ]
      }
      """;

  private static string CreateRelationJson() =>
      """
      {
        "relation": {
          "id": "relation-2",
          "object_id": "topic-2"
        }
      }
      """;
}

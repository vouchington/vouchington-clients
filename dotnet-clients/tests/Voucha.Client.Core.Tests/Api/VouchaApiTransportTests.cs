using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiTransportTests
{
  private static readonly JsonSerializerOptions JsonOptions = VouchaApiJson.Options;

  [Fact]
  public async Task SendAsyncSortsAndEscapesQuery()
  {
    var handler = new RecordingHandler("{\"ok\":true}");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.SendAsync<OkResponse>(
        new ApiRequest(HttpMethod.Get, "/api/v1/test")
        {
          Query = new Dictionary<string, string>(StringComparer.Ordinal)
          {
            ["z"] = "two words",
            ["a"] = "1",
          },
        },
        TestContext.Current.CancellationToken);

    Assert.True(response.Ok);
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal("/api/v1/test?a=1&z=two%20words", handler.PathAndQuery);
  }

  [Fact]
  public async Task SendAsyncThrowsForApiErrors()
  {
    var handler = new RecordingHandler("{\"error\":\"nope\"}", HttpStatusCode.BadRequest);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var exception = await Assert.ThrowsAsync<VouchaApiException>(
        () => client.SendAsync<OkResponse>(
            new ApiRequest(HttpMethod.Delete, "/api/v1/test"),
            TestContext.Current.CancellationToken));

    Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    Assert.Equal("{\"error\":\"nope\"}", exception.ResponseBody);
  }

  [Fact]
  public async Task SendAsyncDiscardsOversizedApiErrors()
  {
    var handler = new RecordingHandler(new string('x', 64 * 1024 + 1), HttpStatusCode.BadRequest);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var exception = await Assert.ThrowsAsync<VouchaApiException>(
        () => client.SendAsync<OkResponse>(
            new ApiRequest(HttpMethod.Get, "/api/v1/test"),
            TestContext.Current.CancellationToken));

    Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    Assert.Null(exception.ResponseBody);
  }

  [Fact]
  public void ConstructorThrowsForNullHttpClient()
  {
    var exception = Assert.Throws<ArgumentNullException>(() => new VouchaApiClient(null!));

    Assert.Equal("httpClient", exception.ParamName);
  }

  [Fact]
  public async Task SendAsyncThrowsForJsonNullBody()
  {
    var handler = new RecordingHandler("null");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var exception = Assert.IsAssignableFrom<HttpRequestException>(await Record.ExceptionAsync(
        () => client.SendAsync<OkResponse>(
            new ApiRequest(HttpMethod.Get, "/api/v1/test"),
            TestContext.Current.CancellationToken)));

    Assert.Equal("Voucha API returned a JSON null response body.", exception.Message);
    Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
  }

  [Fact]
  public async Task SendAsyncWithoutResponseTypeAllowsNoContentSuccess()
  {
    var handler = new NullContentHandler(HttpStatusCode.NoContent);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.SendAsync(
        new ApiRequest(HttpMethod.Post, "/api/v1/auth/logout"),
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Post, handler.Method);
  }

  [Fact]
  public async Task SendAsyncThrowsForMissingSuccessContent()
  {
    var handler = new NullContentHandler(HttpStatusCode.NoContent);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var exception = Assert.IsAssignableFrom<HttpRequestException>(await Record.ExceptionAsync(
        () => client.SendAsync<OkResponse>(
            new ApiRequest(HttpMethod.Get, "/api/v1/test"),
            TestContext.Current.CancellationToken)));

    Assert.Equal("Voucha API returned an empty response body.", exception.Message);
    Assert.Equal(HttpStatusCode.NoContent, exception.StatusCode);
  }

  [Fact]
  public async Task PreviewMarkdownAsyncPostsMarkdownPreviewBody()
  {
    var handler = new RecordingHandler("{\"html\":\"<p>Hello</p>\"}");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.PreviewMarkdownAsync("**Hello**", TestContext.Current.CancellationToken);

    Assert.Equal("<p>Hello</p>", response.Html);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/markdown/preview", handler.PathAndQuery);
    Assert.Contains("\"markdown\":\"**Hello**\"", handler.RequestBody, StringComparison.Ordinal);
  }

  [Fact]
  public async Task SendAsyncThrowsApiExceptionForMissingErrorContent()
  {
    var handler = new NullContentHandler(HttpStatusCode.BadGateway);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var exception = await Assert.ThrowsAsync<VouchaApiException>(
        () => client.SendAsync<OkResponse>(
            new ApiRequest(HttpMethod.Get, "/api/v1/test"),
            TestContext.Current.CancellationToken));

    Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
    Assert.Null(exception.ResponseBody);
  }

  [Theory]
  [InlineData("web.communities.search.default", typeof(CommunitySearchResponse))]
  [InlineData("web.communities.show.default", typeof(CommunityResponse))]
  [InlineData("web.communities.ai-agent.default", typeof(UpsertCommunityAiAgentResponse))]
  [InlineData("web.communities.members.default", typeof(CommunityMembersResponse))]
  [InlineData("web.communities.posts.default", typeof(CommunityPostsResponse))]
  [InlineData("web.communities.news.default", typeof(RssFeedItemsFeedResponse))]
  [InlineData("web.communities.list-items.counts.default", typeof(CommunityListItemCountsResponse))]
  [InlineData("web.communities.list-items.topics.default", typeof(CommunityListTopicsResponse))]
  [InlineData("web.topics.search.default", typeof(TopicSearchResponse))]
  [InlineData("web.topics.mutation.default", typeof(TopicMutationResponse))]
  [InlineData("web.rss-feed-items.feed.default", typeof(RssFeedItemsFeedResponse))]
  [InlineData("swift.posts.feed.default", typeof(PostsFeedResponse))]
  [InlineData("swift.notifications.default", typeof(NotificationsResponse))]
  [InlineData("swift.users.following.default", typeof(UserFollowingResponse))]
  [InlineData("native.users.profile.default", typeof(UserResponse))]
  [InlineData("native.users.profile.restricted", typeof(UserResponse))]
  [InlineData("swift.users.followers.default", typeof(UserFollowersResponse))]
  [InlineData("native.users.delete.default", typeof(DeleteUserResponse))]
  [InlineData("native.users.data-request.default", typeof(UserDataRequestResponse))]
  [InlineData("native.users.data-request.create.default", typeof(UserDataRequestCreationResponse))]
  [InlineData("swift.my.identity.default", typeof(MyIdentityResponse))]
  [InlineData("swift.my.profile.default", typeof(MyProfileResponse))]
  [InlineData("swift.rss-feeds.default", typeof(RssFeedsResponse))]
  [InlineData("swift.rss-feed-items.feed.default", typeof(RssFeedItemsFeedResponse))]
  [InlineData("swift.integration.rss-feed-items.video", typeof(RssFeedItemsFeedResponse))]
  public void DotnetCoreFixturesDeserializeThroughTypedResponses(string fixtureId, Type responseType)
  {
    var response = JsonSerializer.Deserialize(
        ApiFixtureLoader.LoadResponse(fixtureId),
        responseType,
        JsonOptions);

    Assert.NotNull(response);
  }

  [Fact]
  public void CommunityFixturesExposeExpectedTopLevelShapes()
  {
    AssertTopLevelKeys("web.communities.archive.default", "community");
    AssertTopLevelKeys(
        "web.communities.posts.default",
        "bookmarks",
        "communities",
        "page_info",
        "post_link_embeds",
        "posts",
        "posts_metrics",
        "results");
    AssertTopLevelKeys(
        "web.communities.applications.default",
        "community_applications",
        "page_info",
        "results");
    AssertTopLevelKeys(
        "web.communities.invites.default",
        "community_invites",
        "page_info",
        "results");
    AssertTopLevelKeys(
        "web.communities.bans.default",
        "community_bans",
        "page_info",
        "results");
    AssertTopLevelKeys(
        "web.communities.restrictions.default",
        "community_restrictions",
        "page_info",
        "raid_mode_suggestion",
        "results");
    AssertTopLevelKeys(
        "web.communities.moderation-queue.default",
        "entries",
        "page_info",
        "viewer_tier");
    AssertTopLevelKeys(
        "web.communities.moderation-analytics.default",
        "appeals",
        "automod_performance",
        "moderator_workload",
        "queue_volume");
    AssertTopLevelKeys(
        "web.communities.ai-agents.default",
        "community_ai_agents");
    AssertTopLevelKeys(
        "web.communities.agent-prompts.default",
        "community_agent_prompts",
        "slot_info");
    AssertTopLevelKeys(
        "web.communities.automod-simulate.default",
        "results",
        "simulation");
  }

  [Fact]
  public void CommunityListItemFixturesExposeExpectedFields()
  {
    var response = JsonSerializer.Deserialize<CommunityListTopicsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.list-items.topics.default"),
        JsonOptions);

    Assert.NotNull(response);
    Assert.Equal("list-item-topic", response.CommunityListItems["list-item-topic"].Id);
    Assert.Equal(0, response.CommunityListItems["list-item-topic"].OrderIndex);
    Assert.Equal("user-1", response.CommunityListItems["list-item-topic"].AddedById);
  }

  private sealed record OkResponse([property: JsonPropertyName("ok")] bool Ok);

  private static void AssertTopLevelKeys(string fixtureId, params string[] keys)
  {
    using var document = JsonDocument.Parse(ApiFixtureLoader.LoadResponse(fixtureId));
    foreach (var key in keys)
    {
      Assert.True(
          document.RootElement.TryGetProperty(key, out _),
          $"Missing key {key} in fixture {fixtureId}");
    }
  }

  private sealed class NullContentHandler : HttpMessageHandler
  {
    private readonly HttpStatusCode statusCode;

    public HttpMethod? Method { get; private set; }

    public NullContentHandler(HttpStatusCode statusCode)
    {
      this.statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Method = request.Method;
      return Task.FromResult(new HttpResponseMessage(statusCode) { Content = null, RequestMessage = request });
    }
  }
}

using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  public static IEnumerable<object[]> CommunityFetchCases()
  {
    yield return [
        (Func<VouchaApiClient, CancellationToken, Task<object>>)(async (client, token) =>
            await client.FetchCommunityNewsAsync("test community", "cursor", 5, "rss", "query", token)),
        "web.communities.news.default",
        HttpMethod.Get,
        "/api/v1/communities/test%20community/news?after=cursor&feed_type=rss&limit=5&q=query",
    ];
    yield return [
        (Func<VouchaApiClient, CancellationToken, Task<object>>)(async (client, token) =>
            await client.FetchCommunityListTopicsAsync("test-community", "cursor", 3, token)),
        "web.communities.list-items.topics.default",
        HttpMethod.Get,
        "/api/v1/communities/test-community/list-items/topics?after=cursor&limit=3",
    ];
    yield return [
        (Func<VouchaApiClient, CancellationToken, Task<object>>)(async (client, token) =>
            await client.FetchCommunityListRssFeedsAsync("test-community", "cursor", 3, token)),
        "web.communities.list-items.rss-feeds.default",
        HttpMethod.Get,
        "/api/v1/communities/test-community/list-items/rss-feeds?after=cursor&limit=3",
    ];
    yield return [
        (Func<VouchaApiClient, CancellationToken, Task<object>>)(async (client, token) =>
            await client.FetchCommunityListPostsAsync("test-community", "cursor", 3, token)),
        "web.communities.list-items.posts.default",
        HttpMethod.Get,
        "/api/v1/communities/test-community/list-items/posts?after=cursor&limit=3",
    ];
    yield return [
        (Func<VouchaApiClient, CancellationToken, Task<object>>)(async (client, token) =>
            await client.FetchCommunityListDomainsAsync("test-community", "cursor", 3, token)),
        "web.communities.list-items.domains.default",
        HttpMethod.Get,
        "/api/v1/communities/test-community/list-items/domains?after=cursor&limit=3",
    ];
    yield return [
        (Func<VouchaApiClient, CancellationToken, Task<object>>)(async (client, token) =>
            await client.FetchCommunityListUrlsAsync("test-community", "cursor", 3, token)),
        "web.communities.list-items.urls.default",
        HttpMethod.Get,
        "/api/v1/communities/test-community/list-items/urls?after=cursor&limit=3",
    ];
    yield return [
        (Func<VouchaApiClient, CancellationToken, Task<object>>)(async (client, token) =>
            await client.FetchCommunityListItemCountsAsync("test-community", token)),
        "web.communities.list-items.counts.default",
        HttpMethod.Get,
        "/api/v1/communities/test-community/list-items/counts",
    ];
  }

  [Theory]
  [MemberData(nameof(CommunityFetchCases))]
  public async Task CommunityFetchHelpersUseExpectedRoutes(
      Func<VouchaApiClient, CancellationToken, Task<object>> call,
      string fixtureId,
      HttpMethod method,
      string pathAndQuery)
  {
    var (client, handler) = CreateClient(fixtureId);

    var response = await call(client, TestContext.Current.CancellationToken);

    Assert.NotNull(response);
    AssertRequest(handler, method, pathAndQuery);
  }

  [Fact]
  public async Task CommunityMutationHelpersUseExpectedRoutesAndBodies()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.archive.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.archive.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.archive.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.archive.default")),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.CreateCommunityAsync(
        new CreateCommunityRequest(
            "Name",
            "slug",
            "Markdown",
            "public",
            "follow",
            "members",
            true,
            true,
            false,
            true,
            "turnstile"),
        TestContext.Current.CancellationToken);
    await client.UpdateCommunityAsync(
        "test community",
        new UpdateCommunityRequest(
            Name: "Updated",
            Slug: "updated",
            Markdown: JsonNullableString.FromString("Body"),
            Visibility: "private",
            ListType: JsonNullableString.FromString("mute"),
            MemberRosterVisibility: "moderators",
            PostApprovalRequiredAt: false,
            MemberInvitesAllowedAt: false,
            Archive: false),
        TestContext.Current.CancellationToken);
    await client.ArchiveCommunityAsync("test-community", TestContext.Current.CancellationToken);
    await client.UnarchiveCommunityAsync("test-community", TestContext.Current.CancellationToken);
    await client.JoinCommunityAsync("test-community", TestContext.Current.CancellationToken);
    await client.LeaveCommunityAsync("test-community", TestContext.Current.CancellationToken);
    await client.ApplyToCommunityAsync(
        "test-community",
        new ApplyToCommunityRequest(new Dictionary<string, object> { ["why"] = "because" }, "hello"),
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    Assert.Equal("/api/v1/communities", handler.Requests[0].PathAndQuery);
    Assert.Contains("\"cf_turnstile_response\":\"turnstile\"", handler.Requests[0].Body!, StringComparison.Ordinal);
    Assert.Equal(HttpMethod.Patch, handler.Requests[1].Method);
    Assert.Equal("/api/v1/communities/test%20community", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"archive\":false", handler.Requests[1].Body!, StringComparison.Ordinal);
    Assert.Contains("\"member_roster_visibility\":\"moderators\"", handler.Requests[1].Body!, StringComparison.Ordinal);
    Assert.Contains("\"archive\":true", handler.Requests[2].Body!, StringComparison.Ordinal);
    Assert.Contains("\"archive\":false", handler.Requests[3].Body!, StringComparison.Ordinal);
    Assert.Equal(HttpMethod.Post, handler.Requests[4].Method);
    Assert.Equal("/api/v1/communities/test-community/members", handler.Requests[4].PathAndQuery);
    Assert.Equal(HttpMethod.Delete, handler.Requests[5].Method);
    Assert.Equal("/api/v1/communities/test-community/members", handler.Requests[5].PathAndQuery);
    Assert.Equal(HttpMethod.Post, handler.Requests[6].Method);
    Assert.Equal("/api/v1/communities/test-community/applications", handler.Requests[6].PathAndQuery);
    Assert.Contains("\"message\":\"hello\"", handler.Requests[6].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CommunityModeratorStatsUsesExpectedRoute()
  {
    var handler = new RecordingHandler("""
        {"window":7,"stats":[{"actor_user_id":"user-1","total":2,"counts":{"approve":2}}],"users":{}}
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchCommunityModeratorStatsAsync(
        "test-community",
        7,
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/communities/test-community/moderator-stats?window=7");
    Assert.Equal(7, response.Window);
    Assert.Equal("user-1", response.Stats[0].ActorId);
    Assert.Equal(2, response.Stats[0].Counts!["approve"]);
  }

  [Fact]
  public async Task CommunityPinnedPostsUsesPinnedPostsShape()
  {
    var handler = new RecordingHandler("""
        {
          "pinned_posts": [
            {
              "community_id": "community-1",
              "post_id": "post-1",
              "order_index": 0,
              "pinned_by_id": "user-1",
              "created_at": "2026-01-01T00:00:00Z"
            }
          ]
        }
        """);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchCommunityPinnedPostsAsync(
        "test-community",
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/communities/test-community/pinned-posts");
    Assert.Equal("post-1", response.PinnedPosts[0].PostId);
    Assert.Equal(0, response.PinnedPosts[0].OrderIndex);
  }

  [Fact]
  public async Task CommunityServiceDelegatesToCommunityClient()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.archive.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse("{}"),
        new RecordedResponse("{}"),
    ]);
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var detail = await service.FetchDetailAsync("test-community", TestContext.Current.CancellationToken);
    var members = await service.FetchMembersAsync("test-community", TestContext.Current.CancellationToken);
    var posts = await service.FetchPostsAsync("test-community", TestContext.Current.CancellationToken);
    var counts = await service.FetchListItemCountsAsync("test-community", TestContext.Current.CancellationToken);
    var archived = await service.ArchiveAsync("test-community", TestContext.Current.CancellationToken);
    var unarchived = await service.UnarchiveAsync("test-community", TestContext.Current.CancellationToken);
    await service.JoinAsync("test-community", TestContext.Current.CancellationToken);
    await service.LeaveAsync("test-community", TestContext.Current.CancellationToken);

    Assert.Equal("test-community", detail.Community.Slug);
    Assert.Equal("community-member-1", members.Results[0].Id);
    Assert.Equal("post-1", posts.Results[0].Id);
    Assert.Equal(1, counts.Topic);
    Assert.NotNull(archived.Community.ArchivedAt);
    Assert.Null(unarchived.Community.ArchivedAt);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/test-community", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/members?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/posts?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/list-items/counts", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/members", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/test-community/members", request.PathAndQuery));
  }

  [Fact]
  public void CommunityRequestBodiesPreserveCommunityMemberRole()
  {
    var json = JsonSerializer.Serialize(new UpdateCommunityMemberRequest("moderator"), VouchaApiJson.Options);

    Assert.Equal("""{"role":"moderator"}""", json);
  }

  [Fact]
  public void CommunityRequestBodiesOmitNullFields()
  {
    var createJson = JsonSerializer.Serialize(new CreateCommunityRequest("Name"), VouchaApiJson.Options);
    var updateJson = JsonSerializer.Serialize(new UpdateCommunityRequest(Archive: true), VouchaApiJson.Options);
    var clearJson = JsonSerializer.Serialize(
        new UpdateCommunityRequest(Markdown: JsonNullableString.Null, ListType: JsonNullableString.Null),
        VouchaApiJson.Options);
    var itemJson = JsonSerializer.Serialize(new CommunityListItemRequest(UrlId: "url-1"), VouchaApiJson.Options);
    var applyJson = JsonSerializer.Serialize(
        new ApplyToCommunityRequest(new Dictionary<string, object> { ["why"] = "because" }, "hello"),
        VouchaApiJson.Options);

    Assert.Equal("""{"name":"Name"}""", createJson);
    Assert.Equal("""{"archive":true}""", updateJson);
    Assert.Equal("""{"markdown":null,"list_type":null}""", clearJson);
    Assert.Equal("""{"url_id":"url-1"}""", itemJson);
    Assert.Contains("\"answers\":{\"why\":\"because\"}", applyJson, StringComparison.Ordinal);
    Assert.Contains("\"message\":\"hello\"", applyJson, StringComparison.Ordinal);
  }
}

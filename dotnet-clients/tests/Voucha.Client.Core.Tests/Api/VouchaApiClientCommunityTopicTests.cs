using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task SearchCommunitiesAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.communities.search.default");

    var response = await client.SearchCommunitiesAsync(
        new SearchCommunitiesRequest("test"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/communities?q=test");
    Assert.Equal("community-1", response.Results[0].Id);
    Assert.Equal("Test Community", response.Communities["community-1"].Name);
    Assert.Equal(0, response.CommunityMetrics["community-1"].MemberCount);
    Assert.False(response.PageInfo.HasNextPage);
    Assert.Equal("testuser", response.Users["user-1"].Username);
  }

  [Fact]
  public async Task ShowCommunityAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.communities.show.default");

    var response = await client.ShowCommunityAsync(
        new ShowCommunityRequest("test-community"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/communities/test-community");
    Assert.Equal("test-community", response.Community.Slug);
    Assert.Equal("user-1", response.User?.Id);
    Assert.Equal("testuser", response.User?.Username);
    Assert.Equal("community-1", response.CommunityMetrics.Id);
  }

  [Fact]
  public async Task UpsertCommunityAiAgentAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.communities.ai-agent.default");

    var response = await client.UpsertCommunityAiAgentAsync(
        new UpsertCommunityAiAgentRequest("test-community", "self-promotion", new { enabled = true }),
        TestContext.Current.CancellationToken);

    AssertRequest(
        handler,
        HttpMethod.Put,
        "/api/v1/communities/test-community/ai-agents/self-promotion");
    Assert.Contains("\"enabled\":true", handler.RequestBody!, StringComparison.Ordinal);
    Assert.Equal("self-promotion", response.CommunityAiAgent.Slug);
    Assert.Empty(response.CommunityAiAgent.LabelTopicSlugs);
    Assert.Equal("none", response.CommunityAiAgent.OnFlagAction);
    Assert.True(response.CommunityAiAgent.Enabled);
    Assert.False(response.CommunityAiAgent.AlwaysOn);
    Assert.True(response.CommunityAiAgent.Entitlement?.Allowed);
    Assert.Equal("user-1", response.CommunityAiAgent.EnabledById);
  }

  [Fact]
  public async Task FetchCommunityMembersAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.communities.members.default");

    var response = await client.FetchCommunityMembersAsync(
        "test-community",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/communities/test-community/members?limit=20");
    Assert.Equal("community-member-1", response.Results[0].Id);
    Assert.Equal("owner", response.CommunityMembers["community-member-1"].Role);
    Assert.Equal("testuser", response.Users["user-1"].Username);
    Assert.Equal("Test User", response.Users["user-1"].Name);
  }

  [Fact]
  public async Task FetchCommunityPostsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.communities.posts.default");

    var response = await client.FetchCommunityPostsAsync(
        "test-community",
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/communities/test-community/posts?limit=20");
    Assert.Equal("post-1", response.Results[0].Id);
    Assert.Equal("Fixture post", response.Posts["post-1"].Title);
    Assert.Equal("Test Community", response.Communities["community-1"].Name);
    Assert.Equal("test-community", response.Communities["community-1"].Slug);
    Assert.Empty(response.PinnedPostIds ?? []);
  }

  [Fact]
  public async Task SearchTopicsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.topics.search.default");

    var response = await client.SearchTopicsAsync(
        new SearchTopicsRequest("tech"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/topics?q=tech");
    Assert.Equal("topic-1", response.Results[0].Id);
    Assert.Equal("Test Topic", response.Topics["topic-1"].Name);
    Assert.Empty(response.TopicsMetrics);
  }

  [Fact]
  public async Task SearchTopicsAsyncCanFilterReferralPrograms()
  {
    var (client, handler) = CreateClient("web.topics.search.referral-programs.default");

    var response = await client.SearchTopicsAsync(
        new SearchTopicsRequest("test", TopicTypes: "referral_program", Limit: 10),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/topics?limit=10&q=test&topic_types=referral_program");
    Assert.Equal("referral-program-1", response.Results[0].Id);
    Assert.Equal("Test Referral Program", response.Topics["referral-program-1"].Name);
    Assert.Equal("referral_program", response.Topics["referral-program-1"].TopicType);
  }

  [Fact]
  public async Task CreateTopicAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("web.topics.mutation.default");

    var response = await client.CreateTopicAsync(
        new CreateTopicRequest("Tech", "tech", "topic"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/topics");
    Assert.Contains("\"slug\":\"tech\"", handler.RequestBody!, StringComparison.Ordinal);
    Assert.Contains("\"topic_type\":\"topic\"", handler.RequestBody!, StringComparison.Ordinal);
    Assert.Equal("topic-1", response.Topic.Id);
  }

  [Fact]
  public void TopicDeserializesHostnameObjects()
  {
    const string Json = """
        {
          "id": "topic-1",
          "name": "Tech",
          "slug": "tech",
          "topic_type": "topic",
          "hostname": {
            "__entity_type": "hostname",
            "id": "hostname-1",
            "hostname": "example.com",
            "topic_id": "topic-1"
          },
          "hostname_id": "hostname-1"
        }
        """;

    var topic = JsonSerializer.Deserialize<Topic>(Json, VouchaApiJson.Options);

    Assert.NotNull(topic);
    Assert.Equal("example.com", topic.Hostname?.Hostname);
    Assert.Equal("hostname-1", topic.Hostname?.Id);
    Assert.Equal("topic-1", topic.Hostname?.TopicId);
    Assert.Equal("hostname-1", topic.HostnameId);
  }
}

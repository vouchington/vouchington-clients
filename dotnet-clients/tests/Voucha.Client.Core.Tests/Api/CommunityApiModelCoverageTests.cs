using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class CommunityApiModelCoverageTests
{
  [Fact]
  public void CommunityFixturesPopulateOptionalFields()
  {
    var community = JsonSerializer.Deserialize<CommunityResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.archive.default"),
        VouchaApiJson.Options)!.Community;
    var members = JsonSerializer.Deserialize<CommunityMembersResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.members.default"),
        VouchaApiJson.Options)!;
    var posts = JsonSerializer.Deserialize<CommunityPostsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.posts.default"),
        VouchaApiJson.Options)!;

    Assert.Equal("test-community", community.Slug);
    Assert.Equal("public", community.Visibility);
    Assert.Equal("public", community.MemberRosterVisibility);
    Assert.Equal("user-1", community.ArchivedById);
    Assert.NotNull(community.ArchivedAt);
    Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), community.CreatedAt);
    Assert.Equal(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), community.UpdatedAt);
    Assert.Equal("community-member-1", members.Results[0].Id);
    Assert.Equal("community-1", members.CommunityMembers["community-member-1"].CommunityId);
    Assert.Equal("owner", members.CommunityMembers["community-member-1"].Role);
    Assert.Equal("Test User", members.Users["user-1"].Name);
    Assert.Equal("post-1", posts.Results[0].Id);
    Assert.Equal("post-1", posts.Posts["post-1"].Id);
    Assert.Equal(0, posts.PostsMetrics["post-1"].Count.Descendants);
    Assert.NotNull(posts.PostLinkEmbeds);
  }

  [Fact]
  public void CommunityListFixturesPopulateAllListItemShapes()
  {
    var rssFeeds = JsonSerializer.Deserialize<CommunityListRssFeedsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.list-items.rss-feeds.default"),
        VouchaApiJson.Options)!;
    var posts = JsonSerializer.Deserialize<CommunityListPostsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.list-items.posts.default"),
        VouchaApiJson.Options)!;
    var domains = JsonSerializer.Deserialize<CommunityListDomainsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.list-items.domains.default"),
        VouchaApiJson.Options)!;
    var urls = JsonSerializer.Deserialize<CommunityListUrlsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.list-items.urls.default"),
        VouchaApiJson.Options)!;

    Assert.Equal("rss_feed", rssFeeds.CommunityListItems["list-item-rss-feed"].ItemType);
    Assert.Equal("feed-1", rssFeeds.RssFeeds["feed-1"].Id);
    Assert.Equal("post", posts.CommunityListItems["list-item-post"].ItemType);
    Assert.Equal("post-1", posts.Posts["post-1"].Id);
    Assert.Equal(0, posts.PostsMetrics["post-1"].Count.Descendants);
    Assert.Equal("url_hostname", domains.CommunityListItems["list-item-domain"].ItemType);
    Assert.Equal("hostname-1", domains.UrlHostnames["hostname-1"].Id);
    Assert.Equal("url", urls.CommunityListItems["list-item-url"].ItemType);
    Assert.Equal("url-1", urls.Urls["url-1"].Id);
  }

  [Fact]
  public void CommunityRowsUseFallbackValues()
  {
    var member = new CommunityMember("member-1", "community-1", "user-1", "member");
    var post = new Post("post-1", null, null, null, null, Slug: "post-slug");
    var summary = new CommunitySummaryRow("community-1", "Community", "2 members", "public");

    var memberRow = CommunityMemberRow.FromMember(member, null);
    var postRow = CommunityPostRow.FromPost(post, null);

    Assert.Equal("user-1", memberRow.DisplayName);
    Assert.Equal("member", memberRow.ProtocolRole);
    Assert.Equal(UiMessageKey.NativeSwiftPresentationValuesMember, memberRow.RoleText.Key);
    Assert.Equal("Member", memberRow.Role);
    Assert.Equal("post-slug", postRow.Title);
    Assert.Equal("post", postRow.ProtocolPostType);
    Assert.Equal(UiMessageKey.NativeDotnetPostsPostTypePost, postRow.PostTypeText.Key);
    Assert.Equal("Post", postRow.PostType);
    Assert.Equal(0, postRow.ReplyCount);
    Assert.Equal("Community", summary.Title);
    Assert.Equal("public", summary.Detail);
  }

  [Fact]
  public void CommunityAgentAndPostMetricRecordsExposeOptionalFields()
  {
    var agent = new CommunityAiAgent(
        ["topic"],
        "flag",
        "agent-slug",
        "agent-1",
        "user-1",
        "agent",
        true,
        false,
        EnabledAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        EnabledById: "user-2",
        Entitlement: new CommunityAiAgentEntitlement(true, "allowed"));
    var metrics = new PostMetrics(
        "post_metrics",
        "post-1",
        new PostMetricCounts(3, 2, 1),
        UpdatedAt: DateTimeOffset.Parse("2026-01-02T00:00:00Z"),
        Bookmarks: new Dictionary<string, int>
        {
          ["follow"] = 4,
          ["save"] = 5,
        });

    Assert.Equal("topic", agent.LabelTopicSlugs[0]);
    Assert.Equal("user-2", agent.EnabledById);
    Assert.True(agent.Entitlement!.Allowed);
    Assert.Equal("allowed", agent.Entitlement.Reason);
    Assert.Equal(3, metrics.Count.Descendants);
    Assert.Equal(4, metrics.Bookmarks!["follow"]);
    Assert.Equal(5, metrics.Bookmarks["save"]);
  }

  [Fact]
  public void CommunityModerationFixturesPopulateOptionalFields()
  {
    var queue = JsonSerializer.Deserialize<CommunityModerationQueueResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.moderation-queue.default"),
        VouchaApiJson.Options)!;
    var analytics = JsonSerializer.Deserialize<CommunityModerationAnalyticsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.moderation-analytics.default"),
        VouchaApiJson.Options)!;
    var aiAgents = JsonSerializer.Deserialize<CommunityAiAgentsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.ai-agents.default"),
        VouchaApiJson.Options)!;
    var prompts = JsonSerializer.Deserialize<CommunityAgentPromptsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.agent-prompts.default"),
        VouchaApiJson.Options)!;
    var simulation = JsonSerializer.Deserialize<CommunityAutomodSimulation>(
        ApiFixtureLoader.LoadResponse("web.communities.automod-simulate.default"),
        VouchaApiJson.Options)!;
    var history = new CommunityAgentPromptHistoryResponse(
        [
          new CommunityAgentPromptHistoryEntry(
              "history-1",
              "prompt-1",
              "community-1",
              "create",
              null,
              new Dictionary<string, object>(),
              new Dictionary<string, object>(),
              new Dictionary<string, object>(),
              DateTimeOffset.Parse("2026-07-01T00:00:00Z"))
        ],
        "cursor-2");
    var feedback = new CommunityAutomodFeedbackResponse(true);

    Assert.NotEmpty(queue.Entries);
    Assert.NotEmpty(queue.ViewerTier);
    Assert.NotNull(analytics.Scope);
    Assert.NotNull(analytics.QueueVolume);
    Assert.NotNull(analytics.AutomodPerformance);
    Assert.NotNull(analytics.ModeratorWorkload);
    Assert.NotNull(analytics.NewUserFriction);
    Assert.NotEmpty(aiAgents.CommunityAiAgents);
    Assert.NotEmpty(prompts.CommunityAgentPrompts);
    Assert.NotNull(simulation.Results);
    Assert.Equal("cursor-2", history.NextCursor);
    Assert.True(feedback.AppliedAction);
  }

}

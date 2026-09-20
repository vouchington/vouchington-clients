using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NativeRouteCatalogTests
{
  [Fact]
  public void PaidTransparencyDeepLinkIsNavigableForAnAuthenticatedMember()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://moderation-transparency",
        new NavigationViewer(true, []));

    Assert.Equal(NativeDeepLinkStatus.Included, resolution.Status);
    Assert.Equal(NativeRouteDestinationId.ModerationCases, resolution.DestinationId);
    Assert.True(resolution.CanNavigate);
  }

  [Fact]
  public void TopicRecommendationDetailIsTheOnlyRecommendationRouteDispatchedAsPostDetail()
  {
    var detail = NativeRouteCatalog.MatchingRoute("/topic-recommendations/recommendation-1");
    var list = NativeRouteCatalog.MatchingRoute("/topic-recommendations");
    var create = NativeRouteCatalog.MatchingRoute("/topic-recommendations/create");
    var edit = NativeRouteCatalog.MatchingRoute("/topic-recommendations/recommendation-1/edit");

    Assert.True(TopicRecommendationRoute.TryGetDetailId(detail?.Match, out var id));
    Assert.Equal("recommendation-1", id);
    Assert.False(TopicRecommendationRoute.TryGetDetailId(list?.Match, out _));
    Assert.False(TopicRecommendationRoute.TryGetDetailId(create?.Match, out _));
    Assert.False(TopicRecommendationRoute.TryGetDetailId(edit?.Match, out _));
  }

  [Theory]
  [InlineData("/topic/example", NativeRouteDestinationId.TopicDetail, "example")]
  [InlineData("/source/example", NativeRouteDestinationId.SourceDetail, "example")]
  public void TopicAndSourceDetailsShareTheNativeTopicDetailPage(
      string path,
      NativeRouteDestinationId destinationId,
      string expectedTopicId)
  {
    var route = NativeRouteCatalog.MatchingRoute(path);

    Assert.Equal(destinationId, route?.Entry.DestinationId);
    Assert.True(TopicDetailRoute.TryGetTopicId(destinationId, route?.Match, out var topicId));
    Assert.Equal(expectedTopicId, topicId);
  }

  [Theory]
  [InlineData("/feed/posts?sort=new", NativeRouteDestinationId.FeedPosts)]
  [InlineData("/feed/podcasts/sources", NativeRouteDestinationId.FeedPodcasts)]
  [InlineData("/feed/videos/topics", NativeRouteDestinationId.FeedVideos)]
  [InlineData("/feed/referral-links/mutual", NativeRouteDestinationId.FeedReferralLinks)]
  [InlineData("/fediverse", NativeRouteDestinationId.FediverseSearch)]
  [InlineData("/instances", NativeRouteDestinationId.FediverseInstances)]
  [InlineData("/instance/example.social", NativeRouteDestinationId.TopicDetail)]
  [InlineData("/web-search?q=swift", NativeRouteDestinationId.WebSearch)]
  [InlineData("/posts", NativeRouteDestinationId.PostsBrowse)]
  [InlineData("/review/abc123/edit", NativeRouteDestinationId.PostCompose)]
  [InlineData("/links/create", NativeRouteDestinationId.PostCompose)]
  [InlineData("/discussions/create", NativeRouteDestinationId.PostCompose)]
  [InlineData("/articles/create", NativeRouteDestinationId.PostCompose)]
  [InlineData("/blog/create", NativeRouteDestinationId.PostCompose)]
  [InlineData("/article/privacy-policy", NativeRouteDestinationId.PostDetail)]
  [InlineData("/article/terms-of-service", NativeRouteDestinationId.PostDetail)]
  [InlineData("/article/community-guidelines", NativeRouteDestinationId.PostDetail)]
  [InlineData("/review/abc123/comment/comment-1", NativeRouteDestinationId.PostDetail)]
  [InlineData("/discussion/post-1", NativeRouteDestinationId.PostDetail)]
  [InlineData("/story/story-1", NativeRouteDestinationId.PostDetail)]
  [InlineData("/topic/abc123/discussions", NativeRouteDestinationId.TopicDetail)]
  [InlineData("/topics/create", NativeRouteDestinationId.TopicManagement)]
  [InlineData("/topic/abc123/settings/about", NativeRouteDestinationId.TopicManagement)]
  [InlineData("/source/example/latest", NativeRouteDestinationId.SourceDetail)]
  [InlineData("/source/example/referral-links", NativeRouteDestinationId.SourceDetail)]
  [InlineData("/news-sources", NativeRouteDestinationId.FeedNews)]
  [InlineData("/rss-feed-categories", NativeRouteDestinationId.TopicsBrowse)]
  [InlineData("/rewards-program-statuses", NativeRouteDestinationId.TopicsBrowse)]
  [InlineData("/topic-claims/topic-1", NativeRouteDestinationId.TopicsBrowse)]
  [InlineData("/topic-recommendations/recommendation-1", NativeRouteDestinationId.TopicRecommendations)]
  [InlineData("/curated-asides/topics", NativeRouteDestinationId.PostsBrowse)]
  [InlineData("/communities/example/news/sources", NativeRouteDestinationId.CommunityDetail)]
  [InlineData("/landing/alice/featured", NativeRouteDestinationId.LandingPages)]
  [InlineData("/list/list-1", NativeRouteDestinationId.Lists)]
  [InlineData("/url/url-1/crawls/crawl-1", NativeRouteDestinationId.UrlDetail)]
  [InlineData("/crawler/crawler-1", NativeRouteDestinationId.UrlDetail)]
  [InlineData("/user/alice/rss-feeds/following", NativeRouteDestinationId.UserProfile)]
  [InlineData("/user/alice/admin", NativeRouteDestinationId.UserAdmin)]
  [InlineData("/my/posts/saved", NativeRouteDestinationId.Bookmarks)]
  [InlineData("/my/news-items/saved", NativeRouteDestinationId.Bookmarks)]
  [InlineData("/my/domains/blocked", NativeRouteDestinationId.Bookmarks)]
  [InlineData("/reports", NativeRouteDestinationId.ModerationReports)]
  [InlineData("/appeals", NativeRouteDestinationId.ModerationAppeals)]
  [InlineData("/disputes", NativeRouteDestinationId.ModerationDisputes)]
  [InlineData("/my/warnings", NativeRouteDestinationId.ModerationCases)]
  [InlineData("/my/bans", NativeRouteDestinationId.ModerationCases)]
  [InlineData("/my/removed-posts", NativeRouteDestinationId.ModerationCases)]
  [InlineData("/moderation-transparency", NativeRouteDestinationId.ModerationCases)]
  [InlineData("/posts/review-queue", NativeRouteDestinationId.ModerationReviewQueue)]
  [InlineData("/my/notification-settings", NativeRouteDestinationId.AdvancedSettings)]
  [InlineData("/discussion/post-1/tags/topic", NativeRouteDestinationId.TagManagement)]
  [InlineData("/topic/abc123/tags/post", NativeRouteDestinationId.TagManagement)]
  [InlineData("/source/example/tags/publisher_type", NativeRouteDestinationId.TagManagement)]
  [InlineData("/my/notifications", NativeRouteDestinationId.Notifications)]
  [InlineData("/notification-redirect?notification_id=notification-1", NativeRouteDestinationId.Notifications)]
  [InlineData("/messages/new", NativeRouteDestinationId.Messages)]
  [InlineData("/messages/modmail/community-slug/thread-1", NativeRouteDestinationId.Messages)]
  [InlineData("/crm/contact-1", NativeRouteDestinationId.CrmContacts)]
  [InlineData("/admin/modlog", NativeRouteDestinationId.ModerationAdmin)]
  [InlineData("/admin/queues", NativeRouteDestinationId.EngineeringQueues)]
  [InlineData("/admin/postgresql", NativeRouteDestinationId.EngineeringPostgresql)]
  [InlineData("/admin/valkey", NativeRouteDestinationId.EngineeringValkey)]
  [InlineData("/admin/moderation-analytics", NativeRouteDestinationId.ModerationAdmin)]
  [InlineData("/vote-integrity/flags", NativeRouteDestinationId.ModerationIntegrity)]
  [InlineData("/vote-integrity/penalties", NativeRouteDestinationId.ModerationIntegrity)]
  [InlineData("/report-integrity/flags", NativeRouteDestinationId.ModerationIntegrity)]
  [InlineData("/report-integrity/penalties", NativeRouteDestinationId.ModerationIntegrity)]
  [InlineData("/login", NativeRouteDestinationId.SignIn)]
  [InlineData("/compare/apples-vs-oranges", NativeRouteDestinationId.Compare)]
  [InlineData("/domains/compare", NativeRouteDestinationId.DomainsBrowse)]
  public void CatalogResolvesRepresentativeNativeRoutes(string path, NativeRouteDestinationId destinationId)
  {
    var route = NativeRouteCatalog.MatchingRoute(path);

    Assert.NotNull(route);
    Assert.Equal(destinationId, route.Value.Entry.DestinationId);
  }

  [Theory]
  [InlineData("/topics/aliases")]
  [InlineData("/topic/abc123/settings/validations")]
  [InlineData("/admin/users")]
  public void ExcludedRoutesMatchWithoutDestination(string path)
  {
    var route = NativeRouteCatalog.MatchingRoute(path);

    Assert.NotNull(route);
    Assert.Null(route.Value.Entry.DestinationId);
    Assert.NotNull(route.Value.Entry.ExclusionReason);
  }

  [Theory]
  [InlineData("/agents")]
  [InlineData("/agent/helper")]
  [InlineData("/agent/helper/conversation/conversation-1")]
  public void HostedAgentInspectorRoutesAreNotInTheNativeCatalog(string path)
  {
    Assert.Null(NativeRouteCatalog.MatchingRoute(path));
  }

  [Theory]
  [InlineData("/source/example/crawls")]
  [InlineData("/source/example/crawls/crawl-1")]
  public void SourceCrawlRoutesResolveToSourceDetail(string path)
  {
    var route = NativeRouteCatalog.MatchingRoute(path);

    Assert.NotNull(route);
    Assert.Equal(NativeRouteDestinationId.SourceDetail, route.Value.Entry.DestinationId);
    Assert.Equal("Source detail", route.Value.Entry.Family);
  }

  [Fact]
  public void MatchingRoutePreservesPathParametersAndQuery()
  {
    var review = NativeRouteCatalog.MatchingRoute("/review/native-review");
    var newMessage = NativeRouteCatalog.MatchingRoute("/messages/new");
    var users = NativeRouteCatalog.MatchingRoute("/users?q=alice");
    var topic = NativeRouteCatalog.MatchingRoute("/topic/swift/posts");
    var comparison = NativeRouteCatalog.MatchingRoute("/compare/apples-vs-oranges");

    Assert.Equal("native-review", review?.Match.Param("id"));
    Assert.Null(newMessage?.Match.Param("conversationId"));
    Assert.Equal("alice", users?.Match.QueryValue("q"));
    Assert.Equal("swift", topic?.Match.Param("idOrSlug"));
    Assert.Equal("apples", comparison?.Match.Param("slugA"));
    Assert.Equal("oranges", comparison?.Match.Param("slugB"));
  }

  [Fact]
  public void RouteDecodingUsesFormRulesForQueriesAndPreservesPathPlusSigns()
  {
    var match = Assert.IsType<NativeRouteMatch>(new NativeRoutePattern("/example/:idOrSlug")
        .Match("/example/foo+bar?username=alice+smith&post_slug=one%2Btwo"));

    Assert.Equal("foo+bar", match.Param("idOrSlug"));
    Assert.Equal("alice smith", match.QueryValue("username"));
    Assert.Equal("one+two", match.QueryValue("post_slug"));
  }

  [Fact]
  public void SplatPathDecodesPercentEscapesAndPreservesPlusSigns()
  {
    var match = Assert.IsType<NativeRouteMatch>(new NativeRoutePattern("/files/**")
        .Match("/files/foo%2Fbar/a%20b+z"));

    Assert.Equal("foo/bar/a b+z", match.Param("splat"));
  }

  [Fact]
  public void UnknownRouteDoesNotMatch()
  {
    Assert.Null(NativeRouteCatalog.MatchingRoute("/definitely-not-a-native-route"));
  }
}

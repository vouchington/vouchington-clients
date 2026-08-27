using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NativeDeepLinkResolverTests
{
  [Theory]
  [InlineData("administrator")]
  [InlineData("moderator")]
  [InlineData("developer")]
  [InlineData("customer_support")]
  [InlineData("investor")]
  public void DynamicConfigAllowsEveryViewerRole(string role)
  {
    var result = NativeDeepLinkResolver.Resolve(
        "voucha://admin/dynamic-config",
        new NavigationViewer(true, [role]));

    Assert.True(result.CanNavigate);
    Assert.Equal(NativeRouteDestinationId.EngineeringDynamicConfig, result.DestinationId);
  }
  [Theory]
  [InlineData("voucha://feed/news?scope=friends", "/feed/news?scope=friends")]
  [InlineData("voucha://login?emailAddress=a%40example.com&otp=12345678", "/login?emailAddress=a%40example.com&otp=12345678")]
  [InlineData("/topics", "/topics")]
  [InlineData("topics", "/topics")]
  public void TryNormalizeAcceptsVouchaAndRelativeRoutes(string input, string expected)
  {
    Assert.True(NativeDeepLinkResolver.TryNormalize(input, out var pathAndQuery, out var reason));
    Assert.Null(reason);
    Assert.Equal(expected, pathAndQuery);
  }

  [Theory]
  [InlineData("https://example.com/topics")]
  [InlineData("https://voucha.ai/review/post-1")]
  [InlineData("https://staging.voucha.ai/topics")]
  [InlineData("http://voucha.ai/topics")]
  [InlineData("https://localhost/topics")]
  [InlineData("mailto:hello@voucha.ai")]
  public void TryNormalizeRejectsUnsupportedAbsoluteUrls(string input)
  {
    Assert.False(NativeDeepLinkResolver.TryNormalize(input, out _, out var reason));
    Assert.NotNull(reason);
  }

  [Fact]
  public void ResolveQueuesAuthenticatedRoutesForAnonymousViewers()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://feed/news", NavigationViewer.Anonymous);

    Assert.Equal(NativeDeepLinkStatus.Included, resolution.Status);
    Assert.Equal(NativeRouteDestinationId.FeedNews, resolution.DestinationId);
    Assert.Equal(NavigationCatalog.NewsIntentId, resolution.IntentId);
    Assert.True(resolution.RequiresAuthentication);
    Assert.False(resolution.IsVisible);
    Assert.True(resolution.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ResolveAllowsAuthenticatedRoutesForSignedInViewers()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://feed/news",
        new NavigationViewer(true, []));

    Assert.True(resolution.CanNavigate);
    Assert.False(resolution.ShouldQueueUntilAuthenticated);
  }

  [Theory]
  [InlineData("voucha://fediverse", NativeRouteDestinationId.FediverseSearch)]
  [InlineData("voucha://instances", NativeRouteDestinationId.FediverseInstances)]
  [InlineData("voucha://instance/social-example", NativeRouteDestinationId.TopicDetail)]
  public void ResolveMapsFediverseRoutesWhenFeatureFlagIsEnabled(string input, NativeRouteDestinationId destinationId)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        input,
        new NavigationViewer(true, [], new Dictionary<string, bool> { ["fediverse"] = true }));

    Assert.Equal(NativeDeepLinkStatus.Included, resolution.Status);
    Assert.Equal(destinationId, resolution.DestinationId);
    Assert.Equal("fediverse", resolution.IntentId);
    Assert.True(resolution.CanNavigate);
  }

  [Theory]
  [InlineData("voucha://fediverse", NativeRouteDestinationId.FediverseSearch)]
  [InlineData("voucha://instances", NativeRouteDestinationId.FediverseInstances)]
  [InlineData("voucha://instance/social-example", NativeRouteDestinationId.TopicDetail)]
  public void ResolveHidesFediverseRoutesWhenFeatureFlagIsDisabled(string input, NativeRouteDestinationId destinationId)
  {
    var resolution = NativeDeepLinkResolver.Resolve(input, new NavigationViewer(true, []));

    Assert.Equal(destinationId, resolution.DestinationId);
    Assert.Equal("fediverse", resolution.IntentId);
    Assert.False(resolution.IsVisible);
    Assert.False(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveRejectsRoleGatedStaffRoutesForNonStaff()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://crm/contact-1",
        new NavigationViewer(true, []));

    Assert.Equal(NativeRouteDestinationId.CrmContacts, resolution.DestinationId);
    Assert.False(resolution.CanNavigate);
    Assert.False(resolution.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ResolveQueuesRoleGatedStaffRoutesForAnonymousViewers()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://crm/contact-1",
        NavigationViewer.Anonymous);

    Assert.Equal(NativeRouteDestinationId.CrmContacts, resolution.DestinationId);
    Assert.True(resolution.RequiresAuthentication);
    Assert.True(resolution.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ResolveAllowsRoleGatedStaffRoutesForAdministrators()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://crm/contact-1",
        new NavigationViewer(true, ["administrator"]));

    Assert.True(resolution.CanNavigate);
    Assert.Equal("crm", resolution.IntentId);
  }

  [Fact]
  public void ResolveAllowsModerationRoutesForModerators()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://reports",
        new NavigationViewer(true, ["moderator"]));

    Assert.Equal(NativeRouteDestinationId.ModerationReports, resolution.DestinationId);
    Assert.Equal("moderation", resolution.IntentId);
    Assert.True(resolution.CanNavigate);
  }

  [Theory]
  [InlineData("voucha://my/warnings")]
  [InlineData("voucha://my/bans")]
  [InlineData("voucha://my/removed-posts")]
  public void ResolveAllowsAuthenticatedUsersToOpenPersonalModerationCases(string url)
  {
    var resolution = NativeDeepLinkResolver.Resolve(url, new NavigationViewer(true, []));

    Assert.Equal(NativeRouteDestinationId.ModerationCases, resolution.DestinationId);
    Assert.Equal("moderation", resolution.IntentId);
    Assert.True(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveRejectsAdminOnlyModerationRoutesForModerators()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://vote-integrity/flags",
        new NavigationViewer(true, ["moderator"]));

    Assert.Equal(NativeRouteDestinationId.ModerationIntegrity, resolution.DestinationId);
    Assert.Equal("moderation", resolution.IntentId);
    Assert.False(resolution.CanNavigate);
  }

  [Theory]
  [InlineData("moderator")]
  [InlineData("customer_support")]
  [InlineData("member")]
  public void ResolveRejectsIntegrityRoutesForNonAdministratorRoles(string role)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://report-integrity/flags",
        new NavigationViewer(true, [role]));

    Assert.Equal(NativeRouteDestinationId.ModerationIntegrity, resolution.DestinationId);
    Assert.False(resolution.CanNavigate);
  }

  [Theory]
  [InlineData("voucha://report-integrity/flags")]
  [InlineData("voucha://vote-integrity/flags")]
  public void ResolveAllowsIntegrityRoutesForAdministrators(string url)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        url,
        new NavigationViewer(true, ["administrator"]));

    Assert.Equal(NativeRouteDestinationId.ModerationIntegrity, resolution.DestinationId);
    Assert.True(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveRejectsIntegrityRoutesForAnonymousUsers()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://vote-integrity/flags",
        NavigationViewer.Anonymous);

    Assert.False(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveTreatsLoginAsSessionRoute()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://login?emailAddress=a%40example.com&otp=12345678",
        NavigationViewer.Anonymous);

    Assert.True(resolution.CanNavigate);
    Assert.True(resolution.ShouldShowSignIn);
    Assert.Equal("a@example.com", resolution.Match?.QueryValue("emailAddress"));
    Assert.Equal("12345678", resolution.Match?.QueryValue("otp"));
  }

  [Fact]
  public void ResolveHandlesMalformedQueryEscapesWithoutThrowing()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://login?emailAddress=a+b%40example.com&bad%=trailing%",
        NavigationViewer.Anonymous);

    Assert.True(resolution.CanNavigate);
    Assert.Equal("a b@example.com", resolution.Match?.QueryValue("emailAddress"));
    Assert.Equal("trailing%", resolution.Match?.QueryValue("bad%"));
  }

  [Fact]
  public void ResolveMapsReferralsToReferralLinksIntent()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://my/referrals",
        new NavigationViewer(true, []));

    Assert.Equal(NativeRouteDestinationId.Referrals, resolution.DestinationId);
    Assert.Equal("referral-links", resolution.IntentId);
  }

  [Fact]
  public void ResolveMapsMessagesModmailRoutesToMessagesIntent()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://messages/modmail/community-slug/thread-1",
        new NavigationViewer(true, []));

    Assert.Equal(NativeRouteDestinationId.Messages, resolution.DestinationId);
    Assert.Equal(NavigationCatalog.MessagesIntentId, resolution.IntentId);
    Assert.Equal("community-slug", resolution.Match?.Param("communitySlug"));
    Assert.Equal("thread-1", resolution.Match?.Param("threadId"));
  }

  [Fact]
  public void ResolveSettingsRoutesWithoutMisroutingToLists()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://my/identity",
        new NavigationViewer(true, []));

    Assert.Equal(NativeRouteDestinationId.AccountSettings, resolution.DestinationId);
    Assert.Equal(NavigationCatalog.SettingsIntentId, resolution.IntentId);
    Assert.True(resolution.CanNavigate);
  }

  [Theory]
  [InlineData("voucha://my/identity", NativeRouteDestinationId.AccountSettings)]
  [InlineData("voucha://my/profile", NativeRouteDestinationId.ProfileSettings)]
  [InlineData("voucha://my/privacy", NativeRouteDestinationId.AccountSettings)]
  [InlineData("voucha://my/membership", NativeRouteDestinationId.AccountSettings)]
  [InlineData("voucha://my/cards", NativeRouteDestinationId.PaymentCards)]
  [InlineData("voucha://my/spending-categories", NativeRouteDestinationId.SpendingCategories)]
  [InlineData("voucha://my/preferences", NativeRouteDestinationId.AdvancedSettings)]
  [InlineData("voucha://my/api-keys", NativeRouteDestinationId.AdvancedSettings)]
  [InlineData("voucha://my/notification-settings", NativeRouteDestinationId.AdvancedSettings)]
  [InlineData("voucha://my/data", NativeRouteDestinationId.AdvancedSettings)]
  public void ResolveMapsSettingsRoutesToSettingsIntent(string input, NativeRouteDestinationId destinationId)
  {
    var resolution = NativeDeepLinkResolver.Resolve(input, new NavigationViewer(true, []));

    Assert.Equal(destinationId, resolution.DestinationId);
    Assert.Equal(NavigationCatalog.SettingsIntentId, resolution.IntentId);
    Assert.True(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveQueuesUnmappedAuthenticatedRoutesForAnonymousViewers()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://my/identity", NavigationViewer.Anonymous);

    Assert.Equal(NativeRouteDestinationId.AccountSettings, resolution.DestinationId);
    Assert.True(resolution.RequiresAuthentication);
    Assert.True(resolution.ShouldQueueUntilAuthenticated);
  }

  [Theory]
  [InlineData("voucha://sources", NativeRouteDestinationId.SourcesBrowse, "web-search")]
  [InlineData("voucha://news-sources", NativeRouteDestinationId.FeedNews, "news")]
  [InlineData("voucha://podcasts", NativeRouteDestinationId.FeedPodcasts, "podcasts")]
  [InlineData("voucha://podcasts/technology", NativeRouteDestinationId.FeedPodcasts, "podcasts")]
  [InlineData("voucha://channels", NativeRouteDestinationId.FeedVideos, "videos")]
  [InlineData("voucha://my/channels/import-export", NativeRouteDestinationId.ImportExport, "videos")]
  [InlineData("voucha://domains", NativeRouteDestinationId.DomainsBrowse, "web-search")]
  [InlineData("voucha://urls", NativeRouteDestinationId.UrlsBrowse, "web-search")]
  [InlineData("voucha://users", NativeRouteDestinationId.UsersBrowse, "friends")]
  [InlineData("voucha://user/alice", NativeRouteDestinationId.UserProfile, "friends")]
  [InlineData("voucha://user/alice/posts", NativeRouteDestinationId.UserProfile, "friends")]
  [InlineData("voucha://user/alice/users/followers", NativeRouteDestinationId.UserProfile, "friends")]
  [InlineData("voucha://user/alice/topics/following", NativeRouteDestinationId.UserProfile, "friends")]
  [InlineData("voucha://my/friend-recommendations", NativeRouteDestinationId.UsersBrowse, "friends")]
  [InlineData("voucha://chat", NativeRouteDestinationId.Chat, "chat")]
  [InlineData("voucha://chat/support", NativeRouteDestinationId.Support, "chat")]
  [InlineData("voucha://my/landing-pages", NativeRouteDestinationId.LandingPages, "landing-pages")]
  [InlineData("voucha://landing/alice", NativeRouteDestinationId.LandingPages, "landing-pages")]
  [InlineData("voucha://topic-recommendations", NativeRouteDestinationId.TopicRecommendations, "topics")]
  [InlineData("voucha://topic-claims/topic-1", NativeRouteDestinationId.TopicsBrowse, "topics")]
  [InlineData("voucha://topics/create", NativeRouteDestinationId.TopicManagement, "topics")]
  [InlineData("voucha://admin/topic-claims", NativeRouteDestinationId.TopicsBrowse, "topics")]
  [InlineData("voucha://rss-feed-categories", NativeRouteDestinationId.TopicsBrowse, "topics")]
  [InlineData("voucha://rewards-program-statuses", NativeRouteDestinationId.TopicsBrowse, "topics")]
  [InlineData("voucha://referral-programs", NativeRouteDestinationId.FeedReferralLinks, "referral-links")]
  [InlineData("voucha://admin/queues", NativeRouteDestinationId.EngineeringQueues, "engineering")]
  [InlineData("voucha://admin/postgresql", NativeRouteDestinationId.EngineeringPostgresql, "engineering")]
  [InlineData("voucha://admin/valkey", NativeRouteDestinationId.EngineeringValkey, "engineering")]
  [InlineData("voucha://curated-asides/topics", NativeRouteDestinationId.PostsBrowse, "posts")]
  [InlineData("voucha://my/news-items/saved", NativeRouteDestinationId.Bookmarks, "lists")]
  [InlineData("voucha://my/communities/saved", NativeRouteDestinationId.Bookmarks, "lists")]
  [InlineData("voucha://domains/compare", NativeRouteDestinationId.DomainsBrowse, "web-search")]
  [InlineData("voucha://source/example/referral-links", NativeRouteDestinationId.SourceDetail, "web-search")]
  [InlineData("voucha://crawler/crawler-1", NativeRouteDestinationId.UrlDetail, "web-search")]
  [InlineData("voucha://article/privacy-policy", NativeRouteDestinationId.PostDetail, "posts")]
  [InlineData("voucha://review/post-1/comment/comment-1", NativeRouteDestinationId.PostDetail, "posts")]
  [InlineData("voucha://discussion/post-1", NativeRouteDestinationId.PostDetail, "posts")]
  [InlineData("voucha://list/list-1", NativeRouteDestinationId.Lists, "lists")]
  [InlineData("voucha://notification-redirect?notification_id=notification-1", NativeRouteDestinationId.Notifications, "messages")]
  public void ResolveMapsRoutesToCatalogOwningIntent(
      string input,
      NativeRouteDestinationId destinationId,
      string? intentId)
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        input,
        new NavigationViewer(true, []));

    Assert.Equal(destinationId, resolution.DestinationId);
    Assert.Equal(intentId, resolution.IntentId);
  }

  [Fact]
  public void ResolvePreservesCommentPermalinkParameters()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://review/post-1/comment/comment-1", new NavigationViewer(true, []));

    Assert.Equal("post-1", resolution.Match?.Param("id"));
    Assert.Equal("comment-1", resolution.Match?.Param("commentId"));
  }

  [Fact]
  public void ResolveDoesNotQueuePublicLandingAndListLinksForAnonymousViewers()
  {
    var landing = NativeDeepLinkResolver.Resolve("voucha://landing/alice", NavigationViewer.Anonymous);
    var list = NativeDeepLinkResolver.Resolve("voucha://list/list-1", NavigationViewer.Anonymous);

    Assert.False(landing.RequiresAuthentication);
    Assert.False(landing.ShouldQueueUntilAuthenticated);
    Assert.False(list.RequiresAuthentication);
    Assert.False(list.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ResolveDoesNotNavigatePlansWithoutNativeIntent()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://plans", NavigationViewer.Anonymous);

    Assert.Equal(NativeRouteDestinationId.Plans, resolution.DestinationId);
    Assert.Null(resolution.IntentId);
    Assert.False(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveDoesNotQueuePublicUserProfileHistoryLinksForAnonymousViewers()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://user/alice", NavigationViewer.Anonymous);
    var comments = NativeDeepLinkResolver.Resolve("voucha://user/alice/comments", NavigationViewer.Anonymous);

    Assert.Equal(NativeRouteDestinationId.UserProfile, resolution.DestinationId);
    Assert.False(resolution.RequiresAuthentication);
    Assert.False(resolution.ShouldQueueUntilAuthenticated);
    Assert.True(resolution.CanNavigate);
    Assert.False(comments.RequiresAuthentication);
    Assert.False(comments.ShouldQueueUntilAuthenticated);
    Assert.True(comments.CanNavigate);
  }

  [Fact]
  public void ResolveKeepsUserProfileCollectionLinksOnProfileSurface()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://user/alice/users/followers", NavigationViewer.Anonymous);

    Assert.Equal(NativeRouteDestinationId.UserProfile, resolution.DestinationId);
    Assert.Equal("friends", resolution.IntentId);
    Assert.False(resolution.RequiresAuthentication);
    Assert.False(resolution.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ResolveRejectsUnsupportedPublicProfileSourceFilters()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://user/alice/rss-feeds/following?feed_type=music",
        NavigationViewer.Anonymous);

    Assert.Equal(NativeDeepLinkStatus.Invalid, resolution.Status);
    Assert.False(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveAllowsSiteModeratorsToOpenAppeals()
  {
    var resolution = NativeDeepLinkResolver.Resolve(
        "voucha://appeals",
        new NavigationViewer(true, ["moderator"]));

    Assert.Equal(NativeRouteDestinationId.ModerationAppeals, resolution.DestinationId);
    Assert.Equal("moderation", resolution.IntentId);
    Assert.True(resolution.CanNavigate);
  }

  [Fact]
  public void ResolveQueuesPrivateMyRoutesForAnonymousViewers()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://my/posts/saved", NavigationViewer.Anonymous);

    Assert.Equal(NativeRouteDestinationId.Bookmarks, resolution.DestinationId);
    Assert.True(resolution.RequiresAuthentication);
    Assert.True(resolution.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ResolveBlocksAdminTopicRoutesForNonAdministrators()
  {
    var anonymous = NativeDeepLinkResolver.Resolve("voucha://topics/create", NavigationViewer.Anonymous);
    var signedIn = NativeDeepLinkResolver.Resolve("voucha://topics/create", new NavigationViewer(true, []));
    var administrator = NativeDeepLinkResolver.Resolve("voucha://topics/create", new NavigationViewer(true, ["administrator"]));

    Assert.False(anonymous.CanNavigate);
    Assert.True(anonymous.ShouldQueueUntilAuthenticated);
    Assert.False(signedIn.CanNavigate);
    Assert.False(signedIn.ShouldQueueUntilAuthenticated);
    Assert.True(administrator.CanNavigate);
  }

  [Theory]
  [InlineData("voucha://articles/create")]
  [InlineData("voucha://blog/create")]
  public void ResolveBlocksAdminPostComposeRoutesForNonAdministrators(string url)
  {
    var anonymous = NativeDeepLinkResolver.Resolve(url, NavigationViewer.Anonymous);
    var signedIn = NativeDeepLinkResolver.Resolve(url, new NavigationViewer(true, []));
    var administrator = NativeDeepLinkResolver.Resolve(url, new NavigationViewer(true, ["administrator"]));

    Assert.Equal(NativeRouteDestinationId.PostCompose, anonymous.DestinationId);
    Assert.False(anonymous.CanNavigate);
    Assert.True(anonymous.ShouldQueueUntilAuthenticated);
    Assert.False(signedIn.CanNavigate);
    Assert.False(signedIn.ShouldQueueUntilAuthenticated);
    Assert.True(administrator.CanNavigate);
  }

  [Fact]
  public void ResolveQueuesPostEditLinksForAnonymousViewers()
  {
    var resolution = NativeDeepLinkResolver.Resolve("voucha://review/post-1/edit", NavigationViewer.Anonymous);

    Assert.Equal(NativeRouteDestinationId.PostCompose, resolution.DestinationId);
    Assert.Equal("posts", resolution.IntentId);
    Assert.True(resolution.RequiresAuthentication);
    Assert.True(resolution.ShouldQueueUntilAuthenticated);
  }

  [Fact]
  public void ResolveDistinguishesExcludedAndUnmappedRoutes()
  {
    var excluded = NativeDeepLinkResolver.Resolve("voucha://admin/users", NavigationViewer.Anonymous);
    var unmapped = NativeDeepLinkResolver.Resolve("voucha://not-a-route?debug=true", NavigationViewer.Anonymous);

    Assert.Equal(NativeDeepLinkStatus.Excluded, excluded.Status);
    Assert.Equal(NativeDeepLinkStatus.Unmapped, unmapped.Status);
    Assert.Equal("/not-a-route?debug=true", unmapped.PathAndQuery);
  }
}

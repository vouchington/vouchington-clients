using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  [Theory]
  [MemberData(nameof(CoreEndpointCases))]
  public void CoreEndpointsMirrorSwiftRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  [Theory]
  [MemberData(nameof(UserFacingEndpointCases))]
  public void UserFacingEndpointsMirrorSwiftRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  [Theory]
  [MemberData(nameof(SettingsEndpointCases))]
  public void SettingsEndpointsMirrorSwiftRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  [Theory]
  [MemberData(nameof(ModerationEndpointCases))]
  public void ModerationEndpointsMirrorSwiftRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  [Fact]
  public void EndpointBodiesUseBackendJsonFieldNames()
  {
    var body = new CreatePostBody(
        "review",
        "Title",
        "Body",
        "turnstile",
        ReviewTopicRatings: [new CreatePostReviewTopicRatingInput("topic-1", 5)],
        Images: [new CreatePostImageInput("image-1", 1, "caption")]);

    var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    Assert.Contains("\"post_type\":\"review\"", json, StringComparison.Ordinal);
    Assert.Contains("\"review_topic_ratings\"", json, StringComparison.Ordinal);
    Assert.Contains("\"image_id\":\"image-1\"", json, StringComparison.Ordinal);
    Assert.Contains("\"cf_turnstile_response\":\"turnstile\"", json, StringComparison.Ordinal);
    Assert.Contains("\"hp_website\"", json, StringComparison.Ordinal);
  }

  [Fact]
  public void CreateTopicRequestCanSendMarkdownAndHostname()
  {
    var json = JsonSerializer.Serialize(
        new CreateTopicRequest("Name", "slug", "topic", "Markdown", "example.com"),
        VouchaApiJson.Options);

    Assert.Contains("\"markdown\":\"Markdown\"", json, StringComparison.Ordinal);
    Assert.Contains("\"hostname\":\"example.com\"", json, StringComparison.Ordinal);
  }

  [Fact]
  public void UpdatePostBodyCanSendSlug()
  {
    var titleJson = JsonSerializer.Serialize(new UpdatePostBody("Updated"), VouchaApiJson.Options);
    var slugJson = JsonSerializer.Serialize(new UpdatePostBody(Slug: "new-slug"), VouchaApiJson.Options);

    Assert.Equal("""{"title":"Updated"}""", titleJson);
    Assert.Equal("""{"slug":"new-slug"}""", slugJson);
  }

  [Fact]
  public void LandingPageBodiesPreserveExplicitSubtitleNull()
  {
    var createJson = JsonSerializer.Serialize(
        new CreateLandingPageBody("My Links", JsonNullableString.Null, "links"),
        VouchaApiJson.Options);
    var updateJson = JsonSerializer.Serialize(
        new UpdateLandingPageBody(Subtitle: JsonNullableString.Null),
        VouchaApiJson.Options);

    Assert.Contains("\"subtitle\":null", createJson, StringComparison.Ordinal);
    Assert.Contains("\"subtitle\":null", updateJson, StringComparison.Ordinal);
  }

  [Fact]
  public void LandingPageDefaultBodyWritesIsDefaultTrue()
  {
    var json = JsonSerializer.Serialize(new SetLandingPageDefaultBody(), VouchaApiJson.Options);

    Assert.Equal("""{"is_default":true}""", json);
  }

  [Fact]
  public void ProductionJsonOptionsOmitNullRequestFields()
  {
    var body = new CreatePostBody("discussion", "Title", "Body", "turnstile");

    var json = JsonSerializer.Serialize(body, VouchaApiJson.Options);

    Assert.DoesNotContain("\"slug\"", json, StringComparison.Ordinal);
    Assert.DoesNotContain("\"broadcast\"", json, StringComparison.Ordinal);
    Assert.DoesNotContain("\"privacy\"", json, StringComparison.Ordinal);
    Assert.DoesNotContain("\"structured_data\"", json, StringComparison.Ordinal);
    Assert.DoesNotContain("\"data_point_vertical\"", json, StringComparison.Ordinal);
    Assert.Contains("\"cf_turnstile_response\":\"turnstile\"", json, StringComparison.Ordinal);
  }

  [Fact]
  public void UpdateReferralLinkBodyPreservesNullLabelClears()
  {
    var json = JsonSerializer.Serialize(new UpdateReferralLinkBody(null), VouchaApiJson.Options);

    Assert.Equal("""{"label":null}""", json);
  }

  [Fact]
  public void UpdateTopicBodyPreservesExplicitNullClears()
  {
    var body = new UpdateTopicBody(
        Hostname: JsonNullableString.Null,
        LogoImageId: JsonNullableString.Null,
        HeroImageId: JsonNullableString.Null);

    var json = JsonSerializer.Serialize(body, VouchaApiJson.Options);

    Assert.Contains("\"hostname\":null", json, StringComparison.Ordinal);
    Assert.Contains("\"logo_image_id\":null", json, StringComparison.Ordinal);
    Assert.Contains("\"hero_image_id\":null", json, StringComparison.Ordinal);
    Assert.DoesNotContain("\"name\"", json, StringComparison.Ordinal);
  }

  [Fact]
  public void UpdateMyIdentityBodyCanExplicitlyClearProfileImage()
  {
    var json = JsonSerializer.Serialize(
        new UpdateMyIdentityBody(ProfileImageId: JsonNullableString.Null),
        VouchaApiJson.Options);

    Assert.Equal("""{"profile_image_id":null}""", json);
  }

  [Fact]
  public void UpdateUserPrivacyBodyCanExplicitlyClearUiLocale()
  {
    var json = JsonSerializer.Serialize(
        new UpdateUserPrivacyBody(UiLocale: JsonNullableString.Null),
        VouchaApiJson.Options);

    Assert.Equal("""{"ui_locale":null}""", json);
  }

  [Fact]
  public void UpdateAppealRequiresDraftContent()
  {
    var exception = Assert.Throws<ArgumentException>(() => VouchaApiEndpoints.UpdateAppeal("appeal-1"));

    Assert.Equal("publicResponse", exception.ParamName);
  }

  [Fact]
  public void ModerationEnumsDecodeFromProductionStrings()
  {
    const string Json = """
        {
          "appeals": [
            {
              "id": "appeal-1",
              "case_id": "case-1",
              "status": "pending",
              "recommended_action": "accept",
              "post_removal_kind": "community",
              "resolution_action": "deny",
              "created_at": "2026-03-01T11:55:00Z",
              "updated_at": "2026-03-01T12:00:00Z"
            }
          ],
          "page_info": { "has_next_page": false }
        }
        """;

    var response = JsonSerializer.Deserialize<ModerationAppealListResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal(ModerationAppealStatus.Pending, response.Appeals[0].Status);
    Assert.Equal(ModerationAppealAction.Accept, response.Appeals[0].RecommendedAction);
    Assert.Equal(ModerationAppealPostRemovalKind.Community, response.Appeals[0].PostRemovalKind);
    Assert.Equal(ModerationAppealAction.Deny, response.Appeals[0].ResolutionAction);
  }

  [Fact]
  public void ModerationDisputesDecodeFromProductionStrings()
  {
    const string Json = """
        {
          "disputes": [
            {
              "id": "dispute-1",
              "post_id": "post-1",
              "status": "resolved",
              "reason": "I own this content",
              "created_at": "2026-03-01T11:55:00Z",
              "updated_at": "2026-03-01T12:00:00Z"
            }
          ],
          "page_info": { "has_next_page": false }
        }
        """;

    var response = JsonSerializer.Deserialize<ModerationDisputeListResponse>(Json, VouchaApiJson.Options);

    Assert.NotNull(response);
    Assert.Equal("dispute-1", response.Disputes[0].Id);
    Assert.Equal("post-1", response.Disputes[0].PostId);
    Assert.Equal(ModerationDisputeStatus.Resolved, response.Disputes[0].Status);
    Assert.Equal("I own this content", response.Disputes[0].Reason);
  }

  public static IEnumerable<object[]> CoreEndpointCases()
  {
    yield return Case("combinedSearch", VouchaApiEndpoints.CombinedSearch("swift", 11), HttpMethod.Get, "/api/v1/search", Query(("q", "swift"), ("limit", "11")));
    yield return Case("searchCommunities", VouchaApiEndpoints.SearchCommunities("test"), HttpMethod.Get, "/api/v1/communities", Query(("q", "test")));
    yield return Case("showCommunity", VouchaApiEndpoints.ShowCommunity("test community"), HttpMethod.Get, "/api/v1/communities/test%20community", Query());
    yield return Case("upsertCommunityAiAgent", VouchaApiEndpoints.UpsertCommunityAiAgent("test community", "agent 1", new { enabled = true }), HttpMethod.Put, "/api/v1/communities/test%20community/ai-agents/agent%201", Query(), true);
    yield return Case("communityPosts", VouchaApiEndpoints.CommunityPosts("test community", "cursor-1", 13, "hot"), HttpMethod.Get, "/api/v1/communities/test%20community/posts", Query(("limit", "13"), ("sort", "hot"), ("after", "cursor-1")));
    yield return Case("communityMembers", VouchaApiEndpoints.CommunityMembers("test community", "cursor-2", 14), HttpMethod.Get, "/api/v1/communities/test%20community/members", Query(("limit", "14"), ("after", "cursor-2")));
    yield return Case("communityListTopics", VouchaApiEndpoints.CommunityListTopics("test community", "cursor-3", 15), HttpMethod.Get, "/api/v1/communities/test%20community/list-items/topics", Query(("limit", "15"), ("after", "cursor-3")));
    yield return Case("communityListRssFeeds", VouchaApiEndpoints.CommunityListRssFeeds("test community", "cursor-4", 16), HttpMethod.Get, "/api/v1/communities/test%20community/list-items/rss-feeds", Query(("limit", "16"), ("after", "cursor-4")));
    yield return Case("communityListPosts", VouchaApiEndpoints.CommunityListPosts("test community", "cursor-5", 17), HttpMethod.Get, "/api/v1/communities/test%20community/list-items/posts", Query(("limit", "17"), ("after", "cursor-5")));
    yield return Case("communityListDomains", VouchaApiEndpoints.CommunityListDomains("test community", "cursor-6", 18), HttpMethod.Get, "/api/v1/communities/test%20community/list-items/domains", Query(("limit", "18"), ("after", "cursor-6")));
    yield return Case("communityListUrls", VouchaApiEndpoints.CommunityListUrls("test community", "cursor-7", 19), HttpMethod.Get, "/api/v1/communities/test%20community/list-items/urls", Query(("limit", "19"), ("after", "cursor-7")));
    yield return Case("communityListItemCounts", VouchaApiEndpoints.CommunityListItemCounts("test community"), HttpMethod.Get, "/api/v1/communities/test%20community/list-items/counts", Query());
    yield return Case("searchTopics", VouchaApiEndpoints.SearchTopics("tech"), HttpMethod.Get, "/api/v1/topics", Query(("q", "tech")));
    yield return Case("topic", VouchaApiEndpoints.Topic("topic 1"), HttpMethod.Get, "/api/v1/topics/topic%201", Query());
    yield return Case("createTopic", VouchaApiEndpoints.CreateTopic(new CreateTopicRequest("Tech", "tech", "topic")), HttpMethod.Post, "/api/v1/topics", Query(), true);
    yield return Case("updateTopic", VouchaApiEndpoints.UpdateTopic("topic-1", new UpdateTopicBody(Name: "Updated")), HttpMethod.Patch, "/api/v1/topics/topic-1", Query(), true);
    yield return Case("topicTypeAttributes", VouchaApiEndpoints.TopicTypeAttributes("topic-1", "spending-category"), HttpMethod.Get, "/api/v1/topics/topic-1/spending-category", Query());
    yield return Case("updateTopicTypeAttributes", VouchaApiEndpoints.UpdateTopicTypeAttributes("topic-1", "spending-category", new { category = "travel" }), HttpMethod.Patch, "/api/v1/topics/topic-1/spending-category", Query(), true);
    yield return Case("followTopic", VouchaApiEndpoints.FollowTopic("topic-1"), HttpMethod.Put, "/api/v1/bookmarks/topic/topic-1/follow", Query());
    yield return Case("unfollowTopic", VouchaApiEndpoints.UnfollowTopic("topic-1"), HttpMethod.Delete, "/api/v1/bookmarks/topic/topic-1/follow", Query());
    yield return Case("topicAliases", VouchaApiEndpoints.TopicAliases("topic-1", "cursor-1", 12), HttpMethod.Get, "/api/v1/topics/topic-1/aliases", Query(("after", "cursor-1"), ("limit", "12")));
    yield return Case("createTopicAliases", VouchaApiEndpoints.CreateTopicAliases("topic-1", new CreateTopicAliasesBody("alias", true)), HttpMethod.Post, "/api/v1/topics/topic-1/aliases", Query(), true);
    yield return Case("deleteTopicAlias", VouchaApiEndpoints.DeleteTopicAlias("topic-1", "alias 1"), HttpMethod.Delete, "/api/v1/topics/topic-1/aliases/alias%201", Query());
    yield return Case("mergeTopicAliases", VouchaApiEndpoints.MergeTopicAliases("topic-1", "topic-2"), HttpMethod.Post, "/api/v1/topics/topic-1/merges", Query(), true);
    yield return Case("topicAdditionalHostnames", VouchaApiEndpoints.TopicAdditionalHostnames("topic-1", "cursor-2", 13), HttpMethod.Get, "/api/v1/topics/topic-1/additional-hostnames", Query(("after", "cursor-2"), ("limit", "13")));
    yield return Case("createTopicAdditionalHostname", VouchaApiEndpoints.CreateTopicAdditionalHostname("topic-1", "example.com"), HttpMethod.Post, "/api/v1/topics/topic-1/additional-hostnames", Query(), true);
    yield return Case("deleteTopicAdditionalHostname", VouchaApiEndpoints.DeleteTopicAdditionalHostname("topic-1", "hostname 1"), HttpMethod.Delete, "/api/v1/topics/topic-1/additional-hostnames/hostname%201", Query());
    yield return Case("spendingCategoryAttributes", VouchaApiEndpoints.SpendingCategoryAttributes("topic-1"), HttpMethod.Get, "/api/v1/topics/topic-1/spending-category", Query());
    yield return Case("updateSpendingCategoryAttributes", VouchaApiEndpoints.UpdateSpendingCategoryAttributes("topic-1", new { enabled = true }), HttpMethod.Patch, "/api/v1/topics/topic-1/spending-category", Query(), true);
    yield return Case("rssFeedsForTopic", VouchaApiEndpoints.RssFeedsForTopic("topic-1", includeDescendants: true, enabled: true, discoverable: false), HttpMethod.Get, "/api/v1/rss-feeds", Query(("topic", "topic-1"), ("include_descendants", "true"), ("enabled", "true"), ("discoverable", "false")));
    yield return Case("rssFeedsForTopicAllStates", VouchaApiEndpoints.RssFeedsForTopic("topic-1", enabled: RssFeedEnabledFilter.All), HttpMethod.Get, "/api/v1/rss-feeds", Query(("topic", "topic-1"), ("enabled", "null")));
    yield return Case("rssFeedItems", VouchaApiEndpoints.RssFeedItems("any", "cursor-1", 12, "video"), HttpMethod.Get, "/api/v1/feeds/rss_feed_items/any", Query(("limit", "12"), ("after", "cursor-1"), ("media_type", "video")));
    yield return Case("posts", VouchaApiEndpoints.Posts("home", "cursor-2", 13, "discussion"), HttpMethod.Get, "/api/v1/feeds/posts/home", Query(("limit", "13"), ("sort", "hot"), ("after", "cursor-2"), ("post_types", "discussion")));
    yield return Case("notifications", VouchaApiEndpoints.Notifications("cursor-3", 14), HttpMethod.Get, "/api/v1/my/notifications", Query(("limit", "14"), ("after", "cursor-3")));
    yield return Case("myIdentity", VouchaApiEndpoints.MyIdentity(), HttpMethod.Get, "/api/v1/my/identity", Query());
    yield return Case("emailPreferences", VouchaApiEndpoints.EmailPreferences(), HttpMethod.Get, "/api/v1/my/email-preferences", Query());
    yield return Case("updateEmailPreferences", VouchaApiEndpoints.UpdateEmailPreferences(new UpdateEmailPreferencesBody(NewsDigestFrequency: "daily", ModerationEmailCadence: "selected_days", ModerationEmailDaysOfWeek: [1, 3, 5], ModerationEmailTimeOfDay: "09:00", ModerationEmailTimezone: "America/Los_Angeles")), HttpMethod.Patch, "/api/v1/my/email-preferences", Query(), true);
    yield return Case("authSessions", VouchaApiEndpoints.AuthSessions(), HttpMethod.Get, "/api/v1/auth/sessions", Query());
    yield return Case("deleteAuthSession", VouchaApiEndpoints.DeleteAuthSession("session 1"), HttpMethod.Delete, "/api/v1/auth/sessions/session%201", Query());
    yield return Case("revokeAuthSessions", VouchaApiEndpoints.RevokeAuthSessions(), HttpMethod.Post, "/api/v1/auth/sessions/revocations", Query());
    yield return Case("myLandingPages", VouchaApiEndpoints.MyLandingPages(), HttpMethod.Get, "/api/v1/my/landing-pages", Query());
    yield return Case("myLandingPageCandidates", VouchaApiEndpoints.MyLandingPageCandidates(), HttpMethod.Get, "/api/v1/my/landing-pages/candidates", Query());
    yield return Case("myLandingPage", VouchaApiEndpoints.MyLandingPage("landing page 1"), HttpMethod.Get, "/api/v1/my/landing-pages/landing%20page%201", Query());
    yield return Case("createMyLandingPage", VouchaApiEndpoints.CreateMyLandingPage(new CreateLandingPageBody("Title", JsonNullableString.Null, "slug")), HttpMethod.Post, "/api/v1/my/landing-pages", Query(), true);
    yield return Case("updateMyLandingPage", VouchaApiEndpoints.UpdateMyLandingPage("landing page 1", new UpdateLandingPageBody(Title: "Updated")), HttpMethod.Patch, "/api/v1/my/landing-pages/landing%20page%201", Query(), true);
    yield return Case("setDefaultMyLandingPage", VouchaApiEndpoints.SetDefaultMyLandingPage("landing page 1"), HttpMethod.Patch, "/api/v1/my/landing-pages/landing%20page%201", Query(), true);
    yield return Case("deleteMyLandingPage", VouchaApiEndpoints.DeleteMyLandingPage("landing page 1"), HttpMethod.Delete, "/api/v1/my/landing-pages/landing%20page%201", Query());
    yield return Case("replaceMyLandingPageItems", VouchaApiEndpoints.ReplaceMyLandingPageItems("landing page 1", new ReplaceLandingPageItemsBody([new LandingPageLinkItemInput("Newsletter", new Uri("https://example.com/newsletter"))])), HttpMethod.Put, "/api/v1/my/landing-pages/landing%20page%201/items", Query(), true);
    yield return Case("logout", VouchaApiEndpoints.Logout(), HttpMethod.Post, "/api/v1/auth/logout", Query());
    yield return Case("userFollowing", VouchaApiEndpoints.UserFollowing("user 1", 15), HttpMethod.Get, "/api/v1/users/user%201/users/following", Query(("limit", "15")));
    yield return Case("userFollowers", VouchaApiEndpoints.UserFollowers("user-1", 16), HttpMethod.Get, "/api/v1/users/user-1/users/followers", Query(("limit", "16")));
    yield return Case("myProfile", VouchaApiEndpoints.MyProfile(), HttpMethod.Get, "/api/v1/my/profile", Query());
    yield return Case("votePost", VouchaApiEndpoints.VotePost("post-1", ElectionVoteChoice.Like), HttpMethod.Put, "/api/v1/posts/post-1/vote", Query(), true);
    yield return Case("voteTopicRecommendationPost", VouchaApiEndpoints.VotePost("post-1", ElectionVoteChoice.Support), HttpMethod.Put, "/api/v1/posts/post-1/vote", Query(), true);
    yield return Case("voteTopic", VouchaApiEndpoints.VoteTopic("topic-1", ElectionVoteChoice.Dislike), HttpMethod.Put, "/api/v1/topics/topic-1/vote", Query(), true);
    yield return Case("voteRssFeedItem", VouchaApiEndpoints.VoteRssFeedItem("item-1", ElectionVoteChoice.Neutral), HttpMethod.Put, "/api/v1/rss-feed-items/item-1/vote", Query(), true);
    yield return Case("followUser", VouchaApiEndpoints.FollowUser("user-1"), HttpMethod.Put, "/api/v1/bookmarks/user/user-1/follow", Query());
    yield return Case("unfollowUser", VouchaApiEndpoints.UnfollowUser("user-1"), HttpMethod.Delete, "/api/v1/bookmarks/user/user-1/follow", Query());
    yield return Case("followRssFeed", VouchaApiEndpoints.FollowRssFeed("feed-1"), HttpMethod.Put, "/api/v1/bookmarks/rss_feed/feed-1/follow", Query());
    yield return Case("unfollowRssFeed", VouchaApiEndpoints.UnfollowRssFeed("feed-1"), HttpMethod.Delete, "/api/v1/bookmarks/rss_feed/feed-1/follow", Query());
    yield return Case("markNotificationRead", VouchaApiEndpoints.MarkNotificationRead("notification-1"), HttpMethod.Patch, "/api/v1/my/notifications/notification-1", Query());
    yield return Case("notificationRedirectTarget", VouchaApiEndpoints.NotificationRedirectTarget("notification-1"), HttpMethod.Get, "/api/v1/my/notifications/notification-1/redirect-target", Query());
    yield return Case("allRssFeedItems", VouchaApiEndpoints.AllRssFeedItems("cursor-4", 17, "audio"), HttpMethod.Get, "/api/v1/rss-feed-items", Query(("limit", "17"), ("after", "cursor-4"), ("media_type", "audio")));
    yield return Case("allRssFeeds", VouchaApiEndpoints.AllRssFeeds("article", "cursor-5", 18, "news"), HttpMethod.Get, "/api/v1/rss-feeds", Query(("limit", "18"), ("after", "cursor-5"), ("feed_type", "article"), ("category", "news")));
    yield return Case("createSource", VouchaApiEndpoints.CreateSource(new CreateSourceBody(new Uri("https://example.com/feed.xml"), true)), HttpMethod.Post, "/api/v1/rss-feeds", Query(), true);
    yield return Case("updateRssFeed", VouchaApiEndpoints.UpdateRssFeed("feed 1", new { enabled = true }), HttpMethod.Patch, "/api/v1/rss-feeds/feed%201", Query(), true);
    yield return Case("deleteRssFeed", VouchaApiEndpoints.DeleteRssFeed("feed 1"), HttpMethod.Delete, "/api/v1/rss-feeds/feed%201", Query());
    yield return Case("refreshRssFeed", VouchaApiEndpoints.RefreshRssFeed("feed 1", new { force = true }), HttpMethod.Post, "/api/v1/rss-feeds/feed%201/refreshes", Query(), true);
    yield return Case("shareRssFeedItemWithFollowers", VouchaApiEndpoints.ShareRssFeedItemWithFollowers("item 1"), HttpMethod.Post, "/api/v1/rss-feed-items/item%201/shares", Query(), false);
    yield return Case("sendRssFeedItemToFollowers", VouchaApiEndpoints.SendRssFeedItemToFollowers("item 1", FollowerDistributionBody.AllFollowers()), HttpMethod.Post, "/api/v1/rss-feed-items/item%201/sends", Query(), true);
    yield return Case("userRssFeeds", VouchaApiEndpoints.UserRssFeeds("user-1", "following", "podcast", limit: 19), HttpMethod.Get, "/api/v1/users/user-1/rss-feeds/following", Query(("limit", "19"), ("feed_type", "podcast")));
    yield return Case("markAllNotificationsRead", VouchaApiEndpoints.MarkAllNotificationsRead(), HttpMethod.Post, "/api/v1/my/notifications/read-all", Query());
    yield return Case("updateProfile", VouchaApiEndpoints.UpdateProfile("hello"), HttpMethod.Patch, "/api/v1/my/profile", Query(), true);
    yield return Case("voteTopic", VouchaApiEndpoints.VoteTopic("topic-1", ElectionVoteChoice.Dislike), HttpMethod.Put, "/api/v1/topics/topic-1/vote", Query(), true);
    yield return Case("voteRssFeedItem", VouchaApiEndpoints.VoteRssFeedItem("item-1", ElectionVoteChoice.Neutral), HttpMethod.Put, "/api/v1/rss-feed-items/item-1/vote", Query(), true);
    yield return Case("lists", VouchaApiEndpoints.Lists("cursor-1", 20), HttpMethod.Get, "/api/v1/lists", Query(("limit", "20"), ("after", "cursor-1")));
    yield return Case("createList", VouchaApiEndpoints.CreateList(new ListMutationBody("Reading", JsonNullableString.FromString("Saved"), "public")), HttpMethod.Post, "/api/v1/lists", Query(), true);
    yield return Case("list", VouchaApiEndpoints.List("list 1"), HttpMethod.Get, "/api/v1/lists/list%201", Query());
    yield return Case("updateList", VouchaApiEndpoints.UpdateList("list 1", new ListMutationBody(Name: "Watch")), HttpMethod.Patch, "/api/v1/lists/list%201", Query(), true);
    yield return Case("deleteList", VouchaApiEndpoints.DeleteList("list 1"), HttpMethod.Delete, "/api/v1/lists/list%201", Query());
    yield return Case("listItems", VouchaApiEndpoints.ListItems("list 1", "video", false, "cursor-2", 21), HttpMethod.Get, "/api/v1/lists/list%201/items", Query(("limit", "21"), ("media_type", "video"), ("read", "false"), ("after", "cursor-2")));
    yield return Case("addListRssFeedItem", VouchaApiEndpoints.AddListRssFeedItem("list 1", new AddListRssFeedItemBody("item 1")), HttpMethod.Post, "/api/v1/lists/list%201/items/rss-feed-items", Query(), true);
    yield return Case("removeListRssFeedItem", VouchaApiEndpoints.RemoveListRssFeedItem("list 1", "item 1"), HttpMethod.Delete, "/api/v1/lists/list%201/items/rss-feed-items/item%201", Query());
    yield return Case("addListPost", VouchaApiEndpoints.AddListPost("list 1", new AddListPostBody("post 1")), HttpMethod.Post, "/api/v1/lists/list%201/items/posts", Query(), true);
    yield return Case("removeListPost", VouchaApiEndpoints.RemoveListPost("list 1", "post 1"), HttpMethod.Delete, "/api/v1/lists/list%201/items/posts/post%201", Query());
    yield return Case("listsContaining", VouchaApiEndpoints.ListsContaining("rss_feed_item", "item 1"), HttpMethod.Get, "/api/v1/lists/contains", Query(("item_type", "rss_feed_item"), ("entity_id", "item 1")));
    yield return Case("importCommunityList", VouchaApiEndpoints.ImportCommunityList("list 1", new ImportCommunityListBody("community 1")), HttpMethod.Post, "/api/v1/lists/list%201/import", Query(), true);
    yield return Case("markRssFeedItemRead", VouchaApiEndpoints.MarkRssFeedItemRead("item 1"), HttpMethod.Put, "/api/v1/rss-feed-items/item%201/read", Query());
    yield return Case("markRssFeedItemUnread", VouchaApiEndpoints.MarkRssFeedItemUnread("item 1"), HttpMethod.Delete, "/api/v1/rss-feed-items/item%201/read", Query());
    yield return Case("markPostRead", VouchaApiEndpoints.MarkPostRead("post 1"), HttpMethod.Put, "/api/v1/posts/post%201/read", Query());
    yield return Case("markPostUnread", VouchaApiEndpoints.MarkPostUnread("post 1"), HttpMethod.Delete, "/api/v1/posts/post%201/read", Query());
    yield return Case("requestEmailOtp", VouchaApiEndpoints.RequestEmailOtp("test@example.com", "token"), HttpMethod.Post, "/api/v1/auth/email-address/tokens", Query(), true);
    yield return Case("verifyEmailOtp", VouchaApiEndpoints.VerifyEmailOtp("test@example.com", "123456"), HttpMethod.Post, "/api/v1/auth/email-address/login", Query(), true);
    yield return Case("verifyMfaTotp", VouchaApiEndpoints.VerifyMfaTotp("attempt-1", "123456"), HttpMethod.Post, "/api/v1/auth/mfa/totp/verification", Query(), true);
    yield return Case("passkeyAuthOptions", VouchaApiEndpoints.PasskeyAuthOptions(), HttpMethod.Post, "/api/v1/auth/passkeys/authentication/options", Query());
    yield return Case("passkeyAuthVerify", VouchaApiEndpoints.PasskeyAuthVerify(new { id = "credential" }), HttpMethod.Post, "/api/v1/auth/passkeys/authentication/verify", Query(), true);
    yield return Case("appleSignIn", VouchaApiEndpoints.AppleSignIn("token", "nonce", "Alice"), HttpMethod.Post, "/api/v1/auth/oauth/apple/continue", Query(), true);
    yield return Case("createPost", VouchaApiEndpoints.CreatePost(new CreatePostBody("discussion", "Title", "Body", "turnstile")), HttpMethod.Post, "/api/v1/posts", Query(), true);
    yield return Case("post", VouchaApiEndpoints.Post("post 1"), HttpMethod.Get, "/api/v1/posts/post%201", Query());
    yield return Case("postDescendants", VouchaApiEndpoints.PostDescendants("post 1"), HttpMethod.Get, "/api/v1/posts/post%201/descendants", Query());
    yield return Case("postAncestors", VouchaApiEndpoints.PostAncestors("comment 1"), HttpMethod.Get, "/api/v1/posts/comment%201/ancestors", Query());
    yield return Case("updatePost", VouchaApiEndpoints.UpdatePost("post 1", new UpdatePostBody(Title: "Updated")), HttpMethod.Patch, "/api/v1/posts/post%201", Query(), true);
    yield return Case("deletePost", VouchaApiEndpoints.DeletePost("post 1"), HttpMethod.Delete, "/api/v1/posts/post%201", Query());
    yield return Case("lockPost", VouchaApiEndpoints.LockPost("post 1"), HttpMethod.Post, "/api/v1/posts/post%201/lock", Query());
    yield return Case("unlockPost", VouchaApiEndpoints.UnlockPost("post 1"), HttpMethod.Delete, "/api/v1/posts/post%201/lock", Query());
    yield return Case("archivePost", VouchaApiEndpoints.ArchivePost("post 1"), HttpMethod.Patch, "/api/v1/posts/post%201", Query(), true);
    yield return Case("unarchivePost", VouchaApiEndpoints.UnarchivePost("post 1"), HttpMethod.Patch, "/api/v1/posts/post%201", Query(), true);
    yield return Case("addPostRating", VouchaApiEndpoints.AddPostRating("post 1", new AddPostRatingBody("topic-1", 5, 1)), HttpMethod.Post, "/api/v1/posts/post%201/ratings", Query(), true);
    yield return Case("updatePostRating", VouchaApiEndpoints.UpdatePostRating("post 1", "topic 1", new UpdatePostRatingBody(Rating: 4)), HttpMethod.Patch, "/api/v1/posts/post%201/ratings/topic%201", Query(), true);
    yield return Case("deletePostRating", VouchaApiEndpoints.DeletePostRating("post 1", "topic 1"), HttpMethod.Delete, "/api/v1/posts/post%201/ratings/topic%201", Query());
    yield return Case("setPostImages", VouchaApiEndpoints.SetPostImages("post 1", new SetPostImagesBody([new CreatePostImageInput("image-1", 1)])), HttpMethod.Put, "/api/v1/posts/post%201/images", Query(), true);
    yield return Case("createCommunityPost", VouchaApiEndpoints.CreateCommunityPost("community 1", new CreatePostBody("discussion", "Title", "Body", "turnstile")), HttpMethod.Post, "/api/v1/communities/community%201/posts", Query(), true);
    yield return Case("entityRelations", VouchaApiEndpoints.EntityRelations("post", "post 1", "related", "url", limit: 10), HttpMethod.Get, "/api/v1/entity-relations/post/post%201/related/url", Query(("limit", "10"), ("sort", "best")));
    yield return Case("createEntityRelation", VouchaApiEndpoints.CreateEntityRelation("post", "post 1", "related", "url", new CreateEntityRelationBody("url-1")), HttpMethod.Post, "/api/v1/entity-relations/post/post%201/related/url", Query(), true);
    yield return Case("voteEntityRelation", VouchaApiEndpoints.VoteEntityRelation("relation 1", ElectionVoteChoice.Dispute), HttpMethod.Put, "/api/v1/entity-relations/relation%201/vote", Query(), true);
    yield return Case("publisherTypes", VouchaApiEndpoints.PublisherTypes(), HttpMethod.Get, "/api/v1/topics/publisher-types", Query());
    yield return Case("createImageUploadUrl", VouchaApiEndpoints.CreateImageUploadUrl(new CreateImageUploadUrlBody("image/png", 123)), HttpMethod.Post, "/api/v1/images/upload-url", Query(), true);
    yield return Case("completeImageUpload", VouchaApiEndpoints.CompleteImageUpload("image 1"), HttpMethod.Post, "/api/v1/images/image%201/completions", Query());
    yield return Case("imageUploadState", VouchaApiEndpoints.ImageUploadState("image 1"), HttpMethod.Get, "/api/v1/images/image%201/upload-state", Query());
    yield return Case("updatePodcastPlaybackPosition", VouchaApiEndpoints.UpdatePodcastPlaybackPosition("item 1", 12.5, false), HttpMethod.Put, "/api/v1/podcast-episodes/item%201/playback-position", Query(), true);
    yield return Case("podcastPlaybackPosition", VouchaApiEndpoints.PodcastPlaybackPosition("item-1"), HttpMethod.Get, "/api/v1/podcast-episodes/item-1/playback-position", Query());
  }

  public static IEnumerable<object[]> UserFacingEndpointCases()
  {
    yield return Case("webSearch", VouchaApiEndpoints.WebSearch("swift", 11), HttpMethod.Get, "/api/v1/web-search", Query(("query", "swift"), ("limit", "11")));
    yield return Case("featureFlags", VouchaApiEndpoints.FeatureFlags(), HttpMethod.Get, "/api/v1/feature-flags", Query());
    yield return Case("fediverseSearch", VouchaApiEndpoints.FediverseSearch("swift", "peertube,mastodon", "video", 12, "cursor-1"), HttpMethod.Get, "/api/v1/fediverse/search", Query(("q", "swift"), ("providers", "peertube,mastodon"), ("type", "video"), ("limit", "12"), ("after", "cursor-1")));
    yield return Case("trendingCommunities", VouchaApiEndpoints.TrendingCommunities("cursor-1", 12), HttpMethod.Get, "/api/v1/trending-communities", Query(("limit", "12"), ("after", "cursor-1")));
    yield return Case("trendingReferralPrograms", VouchaApiEndpoints.TrendingReferralPrograms("cursor-2", 13), HttpMethod.Get, "/api/v1/trending-referral-programs", Query(("limit", "13"), ("after", "cursor-2")));
    yield return Case("referralLinksFeed", VouchaApiEndpoints.ReferralLinksFeed("any", "cursor-3", 14), HttpMethod.Get, "/api/v1/feeds/referral_links/any", Query(("limit", "14"), ("after", "cursor-3")));
    yield return Case("recommendedTopics", VouchaApiEndpoints.RecommendedTopics("cursor-4", 15, ["rss_feed", "spending_category"], "score", true, false), HttpMethod.Get, "/api/v1/recommended-topics", Query(("limit", "15"), ("after", "cursor-4"), ("topic_types", "rss_feed,spending_category"), ("sort", "score"), ("spending_category", "true"), ("rss_feed", "false")));
    yield return Case("topicRecommendations", VouchaApiEndpoints.TopicRecommendations("cursor-5", 16), HttpMethod.Get, "/api/v1/topic-recommendations", Query(("limit", "16"), ("after", "cursor-5")));
    yield return Case("topicRecommendation", VouchaApiEndpoints.TopicRecommendation("recommendation 1"), HttpMethod.Get, "/api/v1/topic-recommendations/recommendation%201", Query());
    yield return Case("myCommunities", VouchaApiEndpoints.MyCommunities(), HttpMethod.Get, "/api/v1/my/communities", Query());
    yield return Case("myReferralClicks", VouchaApiEndpoints.MyReferralClicks("cursor-6", 17), HttpMethod.Get, "/api/v1/my/referral-clicks", Query(("limit", "17"), ("after", "cursor-6")));
    yield return Case("referralLinks", VouchaApiEndpoints.ReferralLinks("cursor-7", 18), HttpMethod.Get, "/api/v1/referral-links", Query(("limit", "18"), ("after", "cursor-7")));
    yield return Case("createReferralLink", VouchaApiEndpoints.CreateReferralLink(new CreateReferralLinkBody("program-1", new Uri("https://example.com/apply"), "Apply")), HttpMethod.Post, "/api/v1/referral-links", Query(), true);
    yield return Case("updateReferralLink", VouchaApiEndpoints.UpdateReferralLink("link 1", new UpdateReferralLinkBody("Apply")), HttpMethod.Patch, "/api/v1/referral-links/link%201", Query(), true);
    yield return Case("deleteReferralLink", VouchaApiEndpoints.DeleteReferralLink("link 1"), HttpMethod.Delete, "/api/v1/referral-links/link%201", Query());
    yield return Case("activateReferralLink", VouchaApiEndpoints.ActivateReferralLink("link 1"), HttpMethod.Post, "/api/v1/referral-links/link%201/activations", Query(), true);
    yield return Case("deactivateReferralLink", VouchaApiEndpoints.DeactivateReferralLink("link 1"), HttpMethod.Delete, "/api/v1/referral-links/link%201/activations", Query());
    yield return Case("prioritizedReferralLinks", VouchaApiEndpoints.PrioritizedReferralLinks("program 1", all: true), HttpMethod.Get, "/api/v1/topics/program%201/prioritized-referral-links", Query(("all", "true")));
    yield return Case("membershipPlans", VouchaApiEndpoints.MembershipPlans(), HttpMethod.Get, "/api/v1/memberships/plans", Query());
    yield return Case("bookmarks", VouchaApiEndpoints.Bookmarks("post", "post 1"), HttpMethod.Get, "/api/v1/bookmarks/post/post%201", Query());
    yield return Case("bookmark", VouchaApiEndpoints.Bookmark("post", "post-1", "save"), HttpMethod.Put, "/api/v1/bookmarks/post/post-1/save", Query());
    yield return Case("unbookmark", VouchaApiEndpoints.Unbookmark("post", "post-1", "save"), HttpMethod.Delete, "/api/v1/bookmarks/post/post-1/save", Query());
    yield return Case("hostnames", VouchaApiEndpoints.Hostnames(query: "example", after: "cursor-1", limit: 24), HttpMethod.Get, "/api/v1/hostnames", Query(("query", "example"), ("after", "cursor-1"), ("limit", "24")));
    yield return Case("hostname", VouchaApiEndpoints.Hostname("example.com"), HttpMethod.Get, "/api/v1/hostnames/example.com", Query());
    yield return Case("voteHostname", VouchaApiEndpoints.VoteHostname("hostname 1", ElectionVoteChoice.Like), HttpMethod.Put, "/api/v1/hostnames/hostname%201/vote", Query(), true);
    yield return Case("muteHostname", VouchaApiEndpoints.MuteHostname("hostname 1", true), HttpMethod.Put, "/api/v1/bookmarks/url_hostname/hostname%201/mute", Query());
    yield return Case("unmuteHostname", VouchaApiEndpoints.MuteHostname("hostname 1", false), HttpMethod.Delete, "/api/v1/bookmarks/url_hostname/hostname%201/mute", Query());
    yield return Case("blockHostname", VouchaApiEndpoints.BlockHostname("hostname 1", true), HttpMethod.Put, "/api/v1/bookmarks/url_hostname/hostname%201/block", Query());
    yield return Case("unblockHostname", VouchaApiEndpoints.BlockHostname("hostname 1", false), HttpMethod.Delete, "/api/v1/bookmarks/url_hostname/hostname%201/block", Query());
    yield return Case("report", VouchaApiEndpoints.Report(new ReportBody("url_hostname", "hostname-1", "spam", null)), HttpMethod.Post, "/api/v1/reports", Query(), true);
    yield return Case("urls", VouchaApiEndpoints.Urls(query: "example", hostnameId: "hostname-1", after: "cursor-2", limit: 25), HttpMethod.Get, "/api/v1/urls", Query(("query", "example"), ("hostnameId", "hostname-1"), ("after", "cursor-2"), ("limit", "25")));
    yield return Case("userUrls", VouchaApiEndpoints.UserUrls("alice", "saved", 26), HttpMethod.Get, "/api/v1/users/alice/urls/saved", Query(("limit", "26")));
    yield return Case("userHostnames", VouchaApiEndpoints.UserHostnames("alice", "muted", 27), HttpMethod.Get, "/api/v1/users/alice/domains/muted", Query(("limit", "27")));
    yield return Case("userPosts", VouchaApiEndpoints.UserPosts("alice", "saved", limit: 28), HttpMethod.Get, "/api/v1/users/alice/posts/saved", Query(("limit", "28")));
    yield return Case("userTopics", VouchaApiEndpoints.UserTopics("alice", "muted", limit: 29), HttpMethod.Get, "/api/v1/users/alice/topics/muted", Query(("limit", "29")));
    yield return Case("userUsers", VouchaApiEndpoints.UserUsers("alice", "subscribed-posts", limit: 30), HttpMethod.Get, "/api/v1/users/alice/users/subscribed-posts", Query(("limit", "30")));
    yield return Case("userCommunities", VouchaApiEndpoints.UserCommunities("alice", "proxy-following", limit: 31), HttpMethod.Get, "/api/v1/users/alice/communities/proxy-following", Query(("limit", "31")));
    yield return Case("userRssFeedItems", VouchaApiEndpoints.UserRssFeedItems("alice", "saved", mediaType: "article", limit: 32), HttpMethod.Get, "/api/v1/users/alice/rss-feed-items/saved", Query(("limit", "32"), ("media_type", "article")));
    yield return Case("url", VouchaApiEndpoints.Url("url 1"), HttpMethod.Get, "/api/v1/urls/url%201", Query());
    yield return Case("urlCrawls", VouchaApiEndpoints.UrlCrawls("url 1", "cursor-3", 26), HttpMethod.Get, "/api/v1/urls/url%201/crawls", Query(("after", "cursor-3"), ("limit", "26")));
    yield return Case("urlCrawl", VouchaApiEndpoints.UrlCrawl("url 1", "crawl 1"), HttpMethod.Get, "/api/v1/urls/url%201/crawls/crawl%201", Query());
    yield return Case("rssFeedCrawls", VouchaApiEndpoints.RssFeedCrawls("feed 1", "cursor-4", 27), HttpMethod.Get, "/api/v1/rss-feeds/feed%201/crawls", Query(("after", "cursor-4"), ("limit", "27")));
    yield return Case("rssFeedCrawl", VouchaApiEndpoints.RssFeedCrawl("feed 1", "crawl 1"), HttpMethod.Get, "/api/v1/rss-feeds/feed%201/crawls/crawl%201", Query());
    yield return Case("triggerUrlCrawl", VouchaApiEndpoints.TriggerUrlCrawl("url 1"), HttpMethod.Post, "/api/v1/urls/url%201/crawl", Query());
    yield return Case("myConversations", VouchaApiEndpoints.MyConversations("cursor-8", 19), HttpMethod.Get, "/api/v1/my/conversations", Query(("limit", "19"), ("after", "cursor-8")));
    yield return Case("myConversationMessages", VouchaApiEndpoints.MyConversationMessages("conversation-1"), HttpMethod.Get, "/api/v1/my/conversations/conversation-1/messages", Query());
    yield return Case("myMessages", VouchaApiEndpoints.MyMessages("cursor-10", 21), HttpMethod.Get, "/api/v1/my/messages", Query(("limit", "21"), ("after", "cursor-10")));
    yield return Case("createMyMessages", VouchaApiEndpoints.CreateMyMessages(new CreateDirectConversationBody(["user-1", "user-2"])), HttpMethod.Post, "/api/v1/my/messages", Query(), true);
    yield return Case("myMessage", VouchaApiEndpoints.MyMessage("conversation 1"), HttpMethod.Get, "/api/v1/my/messages/conversation%201", Query());
    yield return Case("createMyMessageMessage", VouchaApiEndpoints.CreateMyMessageMessage("conversation 1", new SendDirectMessageBody("Hello")), HttpMethod.Post, "/api/v1/my/messages/conversation%201/messages", Query(), true);
    yield return Case("myMessageConversationMessages", VouchaApiEndpoints.MyMessageConversationMessages("conversation-1", "cursor-11", 22), HttpMethod.Get, "/api/v1/my/messages/conversation-1/messages", Query(("limit", "22"), ("after", "cursor-11")));
    yield return Case("myMessageConversationParticipants", VouchaApiEndpoints.MyMessageConversationParticipants("conversation-1"), HttpMethod.Get, "/api/v1/my/messages/conversation-1/participants", Query());
    yield return Case("addMyMessageConversationParticipant", VouchaApiEndpoints.AddMyMessageConversationParticipant("conversation 1", new AddDirectConversationParticipantBody("user-1")), HttpMethod.Post, "/api/v1/my/messages/conversation%201/participants", Query(), true);
    yield return Case("removeMyMessageConversationParticipant", VouchaApiEndpoints.RemoveMyMessageConversationParticipant("conversation 1", "user 1"), HttpMethod.Delete, "/api/v1/my/messages/conversation%201/participants/user%201", Query());
    yield return Case("updateMyMessageConversationParticipantPolicy", VouchaApiEndpoints.UpdateMyMessageConversationParticipantPolicy("conversation 1", new UpdateDirectConversationParticipantPolicyBody("owner_only")), HttpMethod.Patch, "/api/v1/my/messages/conversation%201", Query(), true);
    yield return Case("searchUsers", VouchaApiEndpoints.SearchUsers("bo", limit: 10), HttpMethod.Get, "/api/v1/users", Query(("q", "bo"), ("limit", "10")));
    yield return Case("searchUsersNextPage", VouchaApiEndpoints.SearchUsers("bo", "cursor-1", 10), HttpMethod.Get, "/api/v1/users", Query(("q", "bo"), ("after", "cursor-1"), ("limit", "10")));
    yield return Case("disputes", VouchaApiEndpoints.Disputes("pending", 23, "cursor-12", true), HttpMethod.Get, "/api/v1/disputes", Query(("limit", "23"), ("status", "pending"), ("after", "cursor-12"), ("mine", "true")));
  }

  public static IEnumerable<object[]> SettingsEndpointCases()
  {
    yield return Case("myIdentity", VouchaApiEndpoints.MyIdentity(), HttpMethod.Get, "/api/v1/my/identity", Query());
    yield return Case("updateMyIdentity", VouchaApiEndpoints.UpdateMyIdentity(new UpdateMyIdentityBody(Username: "alice")), HttpMethod.Patch, "/api/v1/my/identity", Query(), true);
    yield return Case("user", VouchaApiEndpoints.User("user 1"), HttpMethod.Get, "/api/v1/users/user%201", Query());
    yield return Case(
        "updateUser",
        VouchaApiEndpoints.UpdateUser(
            "user 1",
            new UpdateUserPrivacyBody(
                FollowsVisibility: "followers",
                UiLocale: JsonNullableString.FromString("fr"))),
        HttpMethod.Patch,
        "/api/v1/users/user%201",
        Query(),
        true);
    yield return Case("createUserDataRequest", VouchaApiEndpoints.CreateUserDataRequest("user-1"), HttpMethod.Post, "/api/v1/users/user-1/data-request", Query());
    yield return Case("userDataRequest", VouchaApiEndpoints.UserDataRequest("user-1"), HttpMethod.Get, "/api/v1/users/user-1/data-request", Query());
    yield return Case("deleteUser", VouchaApiEndpoints.DeleteUser("user-1"), HttpMethod.Delete, "/api/v1/users/user-1", Query());
    yield return Case("apiKeys", VouchaApiEndpoints.ApiKeys(), HttpMethod.Get, "/api/v1/my/api-keys", Query());
    yield return Case("createApiKey", VouchaApiEndpoints.CreateApiKey(new CreateApiKeyBody("RSS", ["rss-feeds:read"])), HttpMethod.Post, "/api/v1/my/api-keys", Query(), true);
    yield return Case("deleteApiKey", VouchaApiEndpoints.DeleteApiKey("key-1"), HttpMethod.Delete, "/api/v1/my/api-keys/key-1", Query());
    yield return Case("profileLinks", VouchaApiEndpoints.ProfileLinks(), HttpMethod.Get, "/api/v1/my/profile/links", Query());
    yield return Case("createProfileLink", VouchaApiEndpoints.CreateProfileLink(new CreateProfileLinkBody("github", Handle: "alice")), HttpMethod.Post, "/api/v1/my/profile/links", Query(), true);
    yield return Case("reorderProfileLinks", VouchaApiEndpoints.ReorderProfileLinks(new ReorderProfileLinksBody(["link-2", "link-1"])), HttpMethod.Put, "/api/v1/my/profile/links/order", Query(), true);
    yield return Case("updateProfileLink", VouchaApiEndpoints.UpdateProfileLink("link 1", new UpdateProfileLinkBody(Handle: "alice")), HttpMethod.Patch, "/api/v1/my/profile/links/link%201", Query(), true);
    yield return Case("deleteProfileLink", VouchaApiEndpoints.DeleteProfileLink("link 1"), HttpMethod.Delete, "/api/v1/my/profile/links/link%201", Query());
    yield return Case("membershipMe", VouchaApiEndpoints.MembershipMe(), HttpMethod.Get, "/api/v1/memberships/me", Query());
    yield return Case("membershipCheckout", VouchaApiEndpoints.MembershipCheckout(new MembershipCheckoutBody("price-1", new Uri("/my/membership", UriKind.Relative), new Uri("/my/membership", UriKind.Relative))), HttpMethod.Post, "/api/v1/memberships/checkout", Query(), true);
    yield return Case("membershipPortal", VouchaApiEndpoints.MembershipPortal(new MembershipPortalBody(new Uri("/my/membership", UriKind.Relative))), HttpMethod.Post, "/api/v1/memberships/billing-portal-sessions", Query(), true);
    yield return Case("cancelMembership", VouchaApiEndpoints.CancelMembership(), HttpMethod.Delete, "/api/v1/my/membership", Query());
    yield return Case("pushSubscriptions", VouchaApiEndpoints.PushSubscriptions(), HttpMethod.Get, "/api/v1/my/notifications/push-subscriptions", Query());
    yield return Case("deletePushSubscription", VouchaApiEndpoints.DeletePushSubscription("subscription-1"), HttpMethod.Delete, "/api/v1/my/notifications/push-subscriptions/subscription-1", Query());
  }

  public static IEnumerable<object[]> ModerationEndpointCases()
  {
    yield return Case("appeals", VouchaApiEndpoints.Appeals(ModerationAppealStatus.Pending, 11, "cursor-1", true), HttpMethod.Get, "/api/v1/appeals", Query(("limit", "11"), ("status", "pending"), ("after", "cursor-1"), ("mine", "true")));
    yield return Case("appeal", VouchaApiEndpoints.Appeal("appeal-1"), HttpMethod.Get, "/api/v1/appeals/appeal-1", Query());
    yield return Case(
        "submitAppeal",
        VouchaApiEndpoints.SubmitAppeal(new(
            ModerationAppealTargetType.Removal,
            "post-1",
            ModerationAppealReason.Other,
            "because",
            ModerationAppealPostRemovalKind.Community,
            "token")),
        HttpMethod.Post,
        "/api/v1/appeals",
        Query(),
        true);
    yield return Case("updateAppeal", VouchaApiEndpoints.UpdateAppeal("appeal-1", "public", "internal"), HttpMethod.Patch, "/api/v1/appeals/appeal-1", Query(), true);
    yield return Case("appealApproval", VouchaApiEndpoints.AppealApproval("appeal-1"), HttpMethod.Post, "/api/v1/appeals/appeal-1/approval", Query());
    yield return Case("appealDelivery", VouchaApiEndpoints.AppealDelivery("appeal-1"), HttpMethod.Post, "/api/v1/appeals/appeal-1/delivery", Query());
    yield return Case("appealResolution", VouchaApiEndpoints.AppealResolution("appeal-1", ModerationAppealAction.Accept), HttpMethod.Post, "/api/v1/appeals/appeal-1/resolution", Query(), true);
    yield return Case("appealResolutionDrafts", VouchaApiEndpoints.AppealResolutionDrafts("appeal-1"), HttpMethod.Post, "/api/v1/appeals/appeal-1/resolution-drafts", Query());
  }

}

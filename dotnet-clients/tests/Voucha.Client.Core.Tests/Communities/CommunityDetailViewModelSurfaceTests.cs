using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityDetailViewModelSurfaceTests
{
  [Fact]
  public async Task LoadSurfaceAsyncHydratesEachCommunitySection()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.news.default")),
        new RecordedResponse("""
            {
              "pinned_posts": [
                {
                  "community_id": "community-1",
                  "post_id": "post-1",
                  "order_index": 0,
                  "created_at": "2026-07-01T00:00:00Z"
                }
              ]
            }
            """),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.applications.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.invites.default")),
        new RecordedResponse("""
            {
              "window": 30,
              "stats": [
                {
                  "actor_id": "user-1",
                  "total": 2,
                  "counts": { "approve": 2 }
                }
              ],
              "users": {
                "user-1": {
                  "id": "user-1",
                  "username": "owner",
                  "roles": ["user"],
                  "name": "Owner"
                }
              }
            }
            """),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.moderation-queue.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.community.pending-reports.paginated")));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.News, TestContext.Current.CancellationToken);
    Assert.Equal(CommunityDetailSurfaceSection.News, viewModel.SelectedSection);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.PinnedPosts, TestContext.Current.CancellationToken);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Applications, TestContext.Current.CancellationToken);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Invites, TestContext.Current.CancellationToken);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);
    Assert.Equal(CommunityDetailSurfaceSection.Moderation, viewModel.SelectedSection);

    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/community-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/members?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/posts?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/list-items/counts", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/news?limit=25", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/pinned-posts", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/applications?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/invites?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/moderator-stats?window=30", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/moderation-queue?limit=20", request.PathAndQuery),
        request => Assert.Equal(
            "/api/v1/communities/community-1/reports/pending?limit=20&sort=created_at_desc",
            request.PathAndQuery));
  }

  [Fact]
  public async Task LoadSurfaceAsyncHydratesSettingsSection()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.saved-replies.default")));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Settings, TestContext.Current.CancellationToken);

    Assert.Equal(CommunityDetailSurfaceSection.Settings, viewModel.SelectedSection);
    Assert.NotEmpty(viewModel.Moderation);
    Assert.Equal("/api/v1/communities/community-1/saved-replies", handler.Requests[4].PathAndQuery);
  }

  [Fact]
  public async Task LoadSurfaceAsyncHydratesModlogSection()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.modlog.default")));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Modlog, TestContext.Current.CancellationToken);

    Assert.Equal(CommunityDetailSurfaceSection.Modlog, viewModel.SelectedSection);
    Assert.NotEmpty(viewModel.Moderation);
    Assert.Equal("/api/v1/communities/community-1/modlog", handler.Requests[4].PathAndQuery);
  }

  [Fact]
  public async Task LoadSurfaceAsyncHydratesModerationAnalyticsSection()
  {
    var (viewModel, handler) = CreateAdministratorViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.moderation-analytics.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.community.moderation-transparency.default")));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    Assert.Equal(CommunityDetailSurfaceSection.ModerationAnalytics, viewModel.SelectedSection);
    Assert.NotEmpty(viewModel.Moderation);
    Assert.Equal("/api/v1/communities/community-1/moderation-analytics?range=30d", handler.Requests[4].PathAndQuery);
    Assert.Equal("/api/v1/communities/community-1/moderation-transparency?range=30d", handler.Requests[5].PathAndQuery);
  }

  [Fact]
  public async Task LoadSelectedSectionAsyncCapturesSectionFailures()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse("""{"error":"nope"}""", System.Net.HttpStatusCode.BadRequest));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.News, TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Empty(viewModel.News);
    Assert.Equal("/api/v1/communities/community-1/news?limit=25", handler.Requests[4].PathAndQuery);
  }

  [Fact]
  public async Task LoadSelectedSectionAsyncMapsNewsRowsFromReferencedRssFeedItems()
  {
    var (viewModel, _) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse("""
            {
              "results": [{ "__entity_type": "rss_feed_item", "entity_id": "item-1" }],
              "page_info": {
                "end_cursor": null,
                "has_next_page": false,
                "start_cursor": null
              },
              "rss_feed_items": {
                "item-1": {
                  "id": "item-1",
                  "data": {
                    "contentSnippet": "A short snippet",
                    "title": "Hydrated article"
                  },
                  "media_type": "article",
                  "published_at": "2026-07-01T00:00:00Z",
                  "rss_feed": {
                    "id": "feed-1",
                    "title": "Example Feed",
                    "feed_type": "article"
                  },
                  "rss_feed_sources": []
                }
              }
            }
            """));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.News, TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.News);
    Assert.Equal("item-1", row.Id);
    Assert.Equal("Hydrated article", row.Title);
    Assert.Equal("News", row.Subtitle);
    Assert.Equal("Example Feed", row.Detail);
  }

  [Fact]
  public async Task LoadModmailThreadSurfaceAsyncLoadsRequestedThreadMessages()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse("""
            {
              "results": [
                {
                  "id": "message-1",
                  "conversation_id": "thread-1",
                  "body_text": "Please review",
                  "created_by_id": "mod-1",
                  "sender_username": "moderator",
                  "created_at": "2026-07-01T00:00:00Z"
                }
              ],
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """));

    await viewModel.LoadModmailThreadSurfaceAsync("community-1", "thread-1", TestContext.Current.CancellationToken);

    Assert.Equal(CommunityDetailSurfaceSection.Modmail, viewModel.SelectedSection);
    Assert.Collection(
        viewModel.Moderation,
        row =>
        {
          Assert.Equal("message-1", row.Id);
          Assert.Equal("@moderator", row.Title);
          Assert.Equal("Modmail", row.Subtitle);
          Assert.Equal("Please review", row.Detail);
        });
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/community-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/members?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/posts?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/list-items/counts", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/modmail/thread-1/messages", request.PathAndQuery));
  }

  [Fact]
  public async Task LoadModmailThreadSurfaceAsyncCapturesMessageFetchFailures()
  {
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse("""{"error":"nope"}""", System.Net.HttpStatusCode.BadRequest));

    await viewModel.LoadModmailThreadSurfaceAsync("community-1", "thread-1", TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.True(viewModel.HasError);
    Assert.Empty(viewModel.Moderation);
    Assert.Equal("/api/v1/communities/community-1/modmail/thread-1/messages", handler.Requests[4].PathAndQuery);
  }

  [Fact]
  public async Task LoadSurfaceAsyncHydratesRouteSpecificModerationSections()
  {
    var (viewModel, handler) = CreateAdministratorViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.show.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.members.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.posts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.list-items.counts.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.modlog.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.modmail.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("web.communities.moderation-analytics.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.community.moderation-transparency.default")));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Modlog, TestContext.Current.CancellationToken);
    Assert.Equal(CommunityDetailSurfaceSection.Modlog, viewModel.SelectedSection);
    Assert.Contains(viewModel.Moderation, row => row.Id == "modlog-1" && row.Title == "approve");

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Modmail, TestContext.Current.CancellationToken);
    Assert.Equal(CommunityDetailSurfaceSection.Modmail, viewModel.SelectedSection);

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);
    Assert.Equal(CommunityDetailSurfaceSection.ModerationAnalytics, viewModel.SelectedSection);
    Assert.NotEmpty(viewModel.Moderation);

    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/communities/community-1", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/members?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/posts?limit=20", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/list-items/counts", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/modlog", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/modmail", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/moderation-analytics?range=30d", request.PathAndQuery),
        request => Assert.Equal("/api/v1/communities/community-1/moderation-transparency?range=30d", request.PathAndQuery));
  }

  [Fact]
  public async Task SelectSectionAsyncBeforeLoadDoesNotHitTheService()
  {
    var (viewModel, handler) = CreateViewModel();

    await viewModel.SelectSectionAsync(CommunityDetailSurfaceSection.Invites, TestContext.Current.CancellationToken);

    Assert.Equal(CommunityDetailSurfaceSection.Invites, viewModel.SelectedSection);
    Assert.Empty(handler.Requests);
  }

  private static (CommunityDetailViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      params RecordedResponse[] responses)
  {
    return CreateViewModelCore(sessionStore: null, responses);
  }

  private static (CommunityDetailViewModel ViewModel, RecordingHandler Handler) CreateAdministratorViewModel(
      params RecordedResponse[] responses)
  {
    var administrator = new SessionSnapshot(new User(
        Id: "administrator-1",
        Username: "administrator",
        Roles: ["administrator"],
        EmailAddress: "administrator@example.com",
        MembershipPlan: "pro"));
    return CreateViewModelCore(new TestSessionStore(administrator), responses);
  }

  private static (CommunityDetailViewModel ViewModel, RecordingHandler Handler) CreateViewModelCore(
      TestSessionStore? sessionStore,
      RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var service = new ApiCommunitiesService(new VouchaApiClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.test"),
    }));
    return (new CommunityDetailViewModel(service, sessionStore), handler);
  }
}

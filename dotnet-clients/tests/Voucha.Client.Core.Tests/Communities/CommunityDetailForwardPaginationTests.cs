using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityDetailForwardPaginationTests
{
  public static TheoryData<CommunityDetailSurfaceSection> Sections => new()
  {
    CommunityDetailSurfaceSection.Members,
    CommunityDetailSurfaceSection.Posts,
    CommunityDetailSurfaceSection.News,
    CommunityDetailSurfaceSection.Applications,
    CommunityDetailSurfaceSection.Invites,
    CommunityDetailSurfaceSection.Bans,
    CommunityDetailSurfaceSection.Restrictions,
    CommunityDetailSurfaceSection.Modlog,
    CommunityDetailSurfaceSection.Moderation,
  };

  [Theory]
  [MemberData(nameof(Sections))]
  public async Task ForwardPagedSectionsAppendUniqueProjectionsAndForwardCursor(
      CommunityDetailSurfaceSection section)
  {
    var service = new ScriptedCommunitiesService();
    var pages = await CreatePagesAsync(service, section);
    SeedBootstrap(service, section, pages);
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync("community-1", section, TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMoreCommunityList);

    await viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);

    Assert.Equal([pages.FirstId, pages.NextId], ProjectionIds(viewModel, section));
    if (SummaryProjection(viewModel, section) is { } summaries)
    {
      Assert.Single(summaries, row => row.Id == pages.FirstId);
      var appendedSummary = Assert.Single(summaries, row => row.Id == pages.NextId);
      Assert.False(string.IsNullOrWhiteSpace(appendedSummary.Title));
      Assert.False(string.IsNullOrWhiteSpace(appendedSummary.Subtitle));
    }

    var request = Assert.Single(service.ForwardPageRequests, request => request.After == "next-page");
    Assert.Equal(section, request.Section);
    Assert.Equal(ExpectedPageLimit(section), request.Limit);
    Assert.False(viewModel.HasMoreCommunityList);
    Assert.False(viewModel.CanLoadMoreCommunityList);
  }

  [Fact]
  public async Task DelayedApplicationsPageDoesNotReplaceNewlySelectedSection()
  {
    var service = new ScriptedCommunitiesService();
    var pages = await CreatePagesAsync(service, CommunityDetailSurfaceSection.Applications);
    SeedBootstrap(service, CommunityDetailSurfaceSection.Applications, pages);
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.Applications,
        TestContext.Current.CancellationToken);
    var delayedPage = new TaskCompletionSource<CommunityApplicationsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.DelayedApplicationsContinuation = delayedPage.Task;

    var loadMore = viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);
    await service.ApplicationsContinuationStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectSectionAsync(
        CommunityDetailSurfaceSection.Overview,
        TestContext.Current.CancellationToken);
    delayedPage.SetResult((CommunityApplicationsResponse)pages.Continuation);
    await loadMore;

    Assert.Equal(CommunityDetailSurfaceSection.Overview, viewModel.SelectedSection);
    Assert.Equal([pages.FirstId], viewModel.ApplicationRows.Select(row => row.Id));
    Assert.Equal([pages.FirstId], viewModel.Applications.Select(row => row.Id));
    Assert.False(viewModel.IsLoadingMoreCommunityList);
    Assert.False(viewModel.HasMoreCommunityList);
    Assert.False(viewModel.CanLoadMoreCommunityList);
    Assert.False(viewModel.HasCommunityListPaginationError);
  }

  private static void SeedBootstrap(
      ScriptedCommunitiesService service,
      CommunityDetailSurfaceSection section,
      PagePair pages)
  {
    service.DetailResponses.Enqueue(CreateDetailResponse(
        hasMembership: true,
        hasPendingApplication: false,
        membershipRole: "owner"));
    service.CountsResponses.Enqueue(new CommunityListItemCountsResponse(1, 2, 3, 4, 5));
    service.MembersResponses.Enqueue(section == CommunityDetailSurfaceSection.Members
        ? (CommunityMembersResponse)pages.Initial
        : Fixture<CommunityMembersResponse>("web.communities.members.default"));
    service.PostsResponses.Enqueue(section == CommunityDetailSurfaceSection.Posts
        ? (CommunityPostsResponse)pages.Initial
        : Fixture<CommunityPostsResponse>("web.communities.posts.default"));

    switch (section)
    {
      case CommunityDetailSurfaceSection.Members:
        service.MembersResponses.Enqueue((CommunityMembersResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.Posts:
        service.PostsResponses.Enqueue((CommunityPostsResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.News:
        service.NewsResponses.Enqueue((RssFeedItemsFeedResponse)pages.Initial);
        service.NewsResponses.Enqueue((RssFeedItemsFeedResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.Applications:
        service.ApplicationsResponses.Enqueue((CommunityApplicationsResponse)pages.Initial);
        service.ApplicationsResponses.Enqueue((CommunityApplicationsResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.Invites:
        service.InvitesResponses.Enqueue((CommunityInvitesResponse)pages.Initial);
        service.InvitesResponses.Enqueue((CommunityInvitesResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.Bans:
        service.BansResponses.Enqueue((CommunityBansResponse)pages.Initial);
        service.BansResponses.Enqueue((CommunityBansResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.Restrictions:
        service.RestrictionsResponses.Enqueue((CommunityRestrictionsResponse)pages.Initial);
        service.RestrictionsResponses.Enqueue((CommunityRestrictionsResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.Modlog:
        service.ModlogResponses.Enqueue((ModlogResponse)pages.Initial);
        service.ModlogResponses.Enqueue((ModlogResponse)pages.Continuation);
        break;
      case CommunityDetailSurfaceSection.Moderation:
        service.ModerationQueueResponses.Enqueue((CommunityModerationQueueResponse)pages.Initial);
        service.ModerationQueueResponses.Enqueue((CommunityModerationQueueResponse)pages.Continuation);
        break;
    }
  }

  private static async Task<PagePair> CreatePagesAsync(
      ScriptedCommunitiesService service,
      CommunityDetailSurfaceSection section)
  {
    object template = section switch
    {
      CommunityDetailSurfaceSection.Members => Fixture<CommunityMembersResponse>("web.communities.members.default"),
      CommunityDetailSurfaceSection.Posts => Fixture<CommunityPostsResponse>("web.communities.posts.default"),
      CommunityDetailSurfaceSection.News => Fixture<RssFeedItemsFeedResponse>("web.communities.news.default"),
      CommunityDetailSurfaceSection.Applications => await service.FetchApplicationsAsync("community-1"),
      CommunityDetailSurfaceSection.Invites => await service.FetchInvitesAsync("community-1"),
      CommunityDetailSurfaceSection.Bans => await service.FetchBansAsync("community-1"),
      CommunityDetailSurfaceSection.Restrictions => await service.FetchRestrictionsAsync("community-1"),
      CommunityDetailSurfaceSection.Modlog => Fixture<ModlogResponse>("web.communities.modlog.default"),
      CommunityDetailSurfaceSection.Moderation => await service.FetchModerationQueueAsync("community-1"),
      _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };
    return CreatePages(template);
  }

  private static PagePair CreatePages(object template) => template switch
  {
    CommunityMembersResponse page => Pair(page, page.CommunityMembers.Keys.Single(), MemberPages),
    CommunityPostsResponse page => Pair(page, page.Posts.Keys.Single(), PostPages),
    RssFeedItemsFeedResponse page => Pair(page, page.RssFeedItems.Keys.First(), NewsPages),
    CommunityApplicationsResponse page => Pair(page, page.CommunityApplications.Keys.Single(), ApplicationPages),
    CommunityInvitesResponse page => Pair(page, page.CommunityInvites.Keys.Single(), InvitePages),
    CommunityBansResponse page => Pair(page, page.CommunityBans.Keys.Single(), BanPages),
    CommunityRestrictionsResponse page => Pair(page, page.CommunityRestrictions.Keys.Single(), RestrictionPages),
    ModlogResponse page => Pair(page, page.ModeratorActions.Keys.Single(), ModlogPages),
    CommunityModerationQueueResponse page => Pair(page, page.Entries.Single().Id, ModerationPages),
    _ => throw new ArgumentOutOfRangeException(nameof(template)),
  };

  private static PagePair Pair<T>(T template, string firstId, Func<T, string, string, bool, T> create)
      where T : notnull
  {
    var nextId = $"{firstId}-next";
    return new(create(template, firstId, nextId, true), create(template, firstId, nextId, false), firstId, nextId);
  }

  private static CommunityMembersResponse MemberPages(
      CommunityMembersResponse page, string firstId, string nextId, bool initial)
  {
    var first = page.CommunityMembers[firstId];
    var members = new Dictionary<string, CommunityMember>(page.CommunityMembers)
    {
      [nextId] = first with { Id = nextId, UserId = "user-next" },
    };
    return page with
    {
      Results = References(firstId, nextId, initial),
      CommunityMembers = members,
      PageInfo = Info(initial),
    };
  }

  private static CommunityPostsResponse PostPages(
      CommunityPostsResponse page, string firstId, string nextId, bool initial)
  {
    var posts = new Dictionary<string, Post>(page.Posts)
    {
      [nextId] = page.Posts[firstId] with { Id = nextId, Title = "Next post" },
    };
    return page with { Results = References(firstId, nextId, initial), Posts = posts, PageInfo = Info(initial) };
  }

  private static RssFeedItemsFeedResponse NewsPages(
      RssFeedItemsFeedResponse page, string firstId, string nextId, bool initial)
  {
    var items = new Dictionary<string, RssFeedItem>(page.RssFeedItems)
    {
      [nextId] = page.RssFeedItems[firstId] with { Id = nextId },
    };
    return page with { Results = References(firstId, nextId, initial), RssFeedItems = items, PageInfo = Info(initial) };
  }

  private static CommunityApplicationsResponse ApplicationPages(
      CommunityApplicationsResponse page, string firstId, string nextId, bool initial)
  {
    var items = new Dictionary<string, CommunityApplication>(page.CommunityApplications)
    {
      [nextId] = page.CommunityApplications[firstId] with { Id = nextId, UserId = "applicant-next" },
    };
    return page with { Results = References(firstId, nextId, initial), CommunityApplications = items, PageInfo = Info(initial) };
  }

  private static CommunityInvitesResponse InvitePages(
      CommunityInvitesResponse page, string firstId, string nextId, bool initial)
  {
    var items = new Dictionary<string, CommunityInvite>(page.CommunityInvites)
    {
      [nextId] = page.CommunityInvites[firstId] with { Id = nextId, Code = "code-next" },
    };
    return page with { Results = References(firstId, nextId, initial), CommunityInvites = items, PageInfo = Info(initial) };
  }

  private static CommunityBansResponse BanPages(
      CommunityBansResponse page, string firstId, string nextId, bool initial)
  {
    var items = new Dictionary<string, CommunityBan>(page.CommunityBans)
    {
      [nextId] = page.CommunityBans[firstId] with { Id = nextId, UserId = "banned-next" },
    };
    return page with { Results = References(firstId, nextId, initial), CommunityBans = items, PageInfo = Info(initial) };
  }

  private static CommunityRestrictionsResponse RestrictionPages(
      CommunityRestrictionsResponse page, string firstId, string nextId, bool initial)
  {
    var items = new Dictionary<string, CommunityRestriction>(page.CommunityRestrictions)
    {
      [nextId] = page.CommunityRestrictions[firstId] with { Id = nextId, RestrictionType = "slow_mode" },
    };
    return page with { Results = References(firstId, nextId, initial), CommunityRestrictions = items, PageInfo = Info(initial) };
  }

  private static ModlogResponse ModlogPages(ModlogResponse page, string firstId, string nextId, bool initial)
  {
    var items = new Dictionary<string, ModeratorActionView>(page.ModeratorActions)
    {
      [nextId] = page.ModeratorActions[firstId] with { Id = nextId, ActionType = "approve" },
    };
    return page with { Results = References(firstId, nextId, initial), ModeratorActions = items, PageInfo = Info(initial) };
  }

  private static CommunityModerationQueueResponse ModerationPages(
      CommunityModerationQueueResponse page, string firstId, string nextId, bool initial)
  {
    var first = page.Entries.Single();
    return page with
    {
      Entries = initial ? [first] : [first, first, first with { Id = nextId, EntityId = "post-next" }],
      PageInfo = Info(initial),
    };
  }

  private static EntityReference[] References(string firstId, string nextId, bool initial) =>
      initial ? [Reference(firstId)] : [Reference(firstId), Reference(firstId), Reference(nextId)];

  private static EntityReference Reference(string id) =>
      new(null, null, id, null, null, null, null, null, null, null, null, null);

  private static PageInfo Info(bool initial) => new(initial ? "next-page" : null, initial, null);

  private static IReadOnlyList<string> ProjectionIds(
      CommunityDetailViewModel viewModel,
      CommunityDetailSurfaceSection section) => section switch
      {
        CommunityDetailSurfaceSection.Members => viewModel.Members.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.Posts => viewModel.Posts.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.News => viewModel.News.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.Applications => viewModel.ApplicationRows.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.Invites => viewModel.InviteRows.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.Bans => viewModel.BanRows.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.Restrictions => viewModel.RestrictionRows.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.Modlog => viewModel.Moderation.Select(row => row.Id).ToArray(),
        CommunityDetailSurfaceSection.Moderation => viewModel.ModerationRows.Select(row => row.Id).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
      };

  private static IReadOnlyList<CommunitySummaryRow>? SummaryProjection(
      CommunityDetailViewModel viewModel,
      CommunityDetailSurfaceSection section) => section switch
      {
        CommunityDetailSurfaceSection.Applications => viewModel.Applications,
        CommunityDetailSurfaceSection.Invites => viewModel.Invites,
        CommunityDetailSurfaceSection.Bans or
        CommunityDetailSurfaceSection.Restrictions or
        CommunityDetailSurfaceSection.Modlog or
        CommunityDetailSurfaceSection.Moderation => viewModel.Moderation,
        _ => null,
      };

  private static int? ExpectedPageLimit(CommunityDetailSurfaceSection section) => section switch
  {
    CommunityDetailSurfaceSection.Members => 20,
    CommunityDetailSurfaceSection.Posts => 20,
    CommunityDetailSurfaceSection.News => 25,
    CommunityDetailSurfaceSection.Applications => 20,
    CommunityDetailSurfaceSection.Invites => 20,
    CommunityDetailSurfaceSection.Bans => 20,
    CommunityDetailSurfaceSection.Restrictions => null,
    CommunityDetailSurfaceSection.Modlog => null,
    CommunityDetailSurfaceSection.Moderation => 20,
    _ => throw new ArgumentOutOfRangeException(nameof(section)),
  };

  private static T Fixture<T>(string id) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(id), VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"{id} did not deserialize as {typeof(T).Name}.");

  private sealed record PagePair(object Initial, object Continuation, string FirstId, string NextId);
}

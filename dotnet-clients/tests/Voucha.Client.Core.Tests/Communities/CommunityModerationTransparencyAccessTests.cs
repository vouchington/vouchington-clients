using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityModerationTransparencyAccessTests
{
  [Fact]
  public async Task RegularMemberLoadsOnlyPaidCommunityTransparency()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanModerateCommunity);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("transparency-empty", viewModel.Moderation.Single().Id);
  }

  [Fact]
  public async Task CommunityTransparencyRangeSelectionReloadsTheRequestedPeriodAfterAnInitialDeepLinkRange()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);
    await viewModel.SelectModerationTransparencyRangeAsync("7d", TestContext.Current.CancellationToken);

    Assert.Equal("7d", viewModel.ModerationTransparencyRange);
    Assert.Equal("7d", service.ModerationTransparencyRange);
  }

  [Fact]
  public async Task AllTimeCommunityTransparencyFormatsMonthlyCohortsAsMonthAndYear()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyResponses.Enqueue(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)]));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    viewModel.SetModerationTransparencyRange("all");

    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    Assert.Contains("August 2026", viewModel.Moderation.Single().Detail);
    Assert.DoesNotContain("8/1/2026", viewModel.Moderation.Single().Detail, StringComparison.Ordinal);
  }

  [Fact]
  public async Task MissingTransparencyEndpointShowsUnavailableState()
  {
    var service = new ScriptedCommunitiesService
    {
      ModerationTransparencyFailure = new VouchaApiException(HttpStatusCode.NotFound, "{}"),
    };
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("transparency-locked", viewModel.Moderation.Single().Id);
  }

  [Fact]
  public async Task EntitlementLossClearsTheCommunityTransparencyCursor()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyResponses.Enqueue(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
        "older-page"));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanLoadMoreTransparency);

    service.ModerationTransparencyFailure = new VouchaApiException(HttpStatusCode.Forbidden, "{}");
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanLoadMoreTransparency);
    Assert.Null(viewModel.TransparencyPaginationError);
    Assert.Equal("transparency-locked", viewModel.Moderation.Single().Id);
  }

  [Theory]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.NotFound)]
  public async Task PaidTransparencyContinuationDenialReplacesBucketsWithLockedState(HttpStatusCode statusCode)
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyResponses.Enqueue(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
        "older-page"));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    service.ModerationTransparencyFailure = new VouchaApiException(statusCode, "{}");
    await viewModel.LoadMoreTransparencyAsync(TestContext.Current.CancellationToken);

    Assert.Equal("transparency-locked", viewModel.Moderation.Single().Id);
    Assert.False(viewModel.CanLoadMoreTransparency);
    Assert.Null(viewModel.TransparencyPaginationError);
  }

  [Fact]
  public async Task SiteModeratorContinuationForbiddenPreservesRawRowsAndReplacesPaidBucketsWithLockedState()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyHandler = (_, range, after, _) => after is null
        ? Task.FromResult(new ModerationTransparencyResponse(
            range,
            [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
            "older-page"))
        : Task.FromException<ModerationTransparencyResponse>(new VouchaApiException(HttpStatusCode.Forbidden, "{}"));
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var session = new SessionSnapshot(new User(
        Id: "moderator-1", Username: "moderator", Roles: ["moderator"],
        EmailAddress: "moderator@example.com", MembershipPlan: "free"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(session));
    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    await viewModel.LoadMoreTransparencyAsync(TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.Moderation, row => row.Id == "queue-volume");
    Assert.Contains(viewModel.Moderation, row => row.Id == "transparency-locked");
    Assert.DoesNotContain(viewModel.Moderation, row => row.Id == "transparency-2026-08-01-reports-spam");
    Assert.False(viewModel.CanLoadMoreTransparency);
    Assert.Null(viewModel.TransparencyPaginationError);
  }

  [Fact]
  public async Task SiteModeratorContinuationMissingEndpointClearsRawRows()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyHandler = (_, range, after, _) => after is null
        ? Task.FromResult(new ModerationTransparencyResponse(
            range,
            [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
            "older-page"))
        : Task.FromException<ModerationTransparencyResponse>(new VouchaApiException(HttpStatusCode.NotFound, "{}"));
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var session = new SessionSnapshot(new User(
        Id: "moderator-1", Username: "moderator", Roles: ["moderator"],
        EmailAddress: "moderator@example.com", MembershipPlan: "free"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(session));
    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    await viewModel.LoadMoreTransparencyAsync(TestContext.Current.CancellationToken);

    Assert.Equal("transparency-locked", viewModel.Moderation.Single().Id);
    Assert.False(viewModel.CanLoadMoreTransparency);
    Assert.Null(viewModel.TransparencyPaginationError);
  }

  [Fact]
  public async Task TransientPaidTransparencyContinuationRaisesItsDedicatedVisibleError()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyHandler = (_, range, after, _) => after is null
        ? Task.FromResult(new ModerationTransparencyResponse(
            range,
            [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
            "older-page"))
        : Task.FromException<ModerationTransparencyResponse>(new HttpRequestException("older page unavailable"));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    viewModel.SetModerationTransparencyRange("all");
    var changed = new List<string?>();
    viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreTransparencyAsync(TestContext.Current.CancellationToken);

    Assert.Equal("older page unavailable", viewModel.TransparencyPaginationError);
    Assert.Contains(nameof(CommunityDetailViewModel.TransparencyPaginationError), changed);
  }

  [Fact]
  public async Task StaleContinuationDenialCannotReplaceNewerTransparencyRange()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyResponses.Enqueue(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
        "older-page"));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    var continuationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continuation = new TaskCompletionSource<ModerationTransparencyResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.ModerationTransparencyHandler = (_, range, after, _) =>
    {
      if (range == "all" && after == "older-page")
      {
        continuationStarted.SetResult();
        return continuation.Task;
      }
      return Task.FromResult(new ModerationTransparencyResponse(
          range,
          [new ModerationTransparencyBucket("2026-08-14", "reports", "harassment", 25)]));
    };

    var loadOlder = viewModel.LoadMoreTransparencyAsync(TestContext.Current.CancellationToken);
    await continuationStarted.Task;
    await viewModel.SelectModerationTransparencyRangeAsync("7d", TestContext.Current.CancellationToken);
    continuation.SetException(new VouchaApiException(HttpStatusCode.Forbidden, "{}"));
    await loadOlder;

    Assert.Equal("7d", viewModel.ModerationTransparencyRange);
    Assert.Equal("transparency-2026-08-14-reports-harassment", viewModel.Moderation.Single().Id);
    Assert.Null(viewModel.TransparencyPaginationError);
  }

  [Fact]
  public async Task SameRangeReloadDiscardsAnInFlightAllTimeContinuation()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyResponses.Enqueue(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
        "older-page"));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    var continuationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var continuationResponse = new TaskCompletionSource<ModerationTransparencyResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.ModerationTransparencyHandler = (_, range, after, _) =>
    {
      if (after == "older-page")
      {
        continuationStarted.SetResult();
        return continuationResponse.Task;
      }
      return Task.FromResult(new ModerationTransparencyResponse(
          range,
          [new ModerationTransparencyBucket("2026-07-01", "reports", "harassment", 25)],
          "older-page"));
    };

    var loadOlder = viewModel.LoadMoreTransparencyAsync(TestContext.Current.CancellationToken);
    await continuationStarted.Task;
    await viewModel.SelectModerationTransparencyRangeAsync("all", TestContext.Current.CancellationToken);
    continuationResponse.SetResult(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2025-08-01", "reports", "spam", 30)],
        "oldest-page"));
    await loadOlder;

    Assert.Equal("all", viewModel.ModerationTransparencyRange);
    Assert.Single(viewModel.Moderation);
    Assert.Contains("Harassment", viewModel.Moderation.Single().Detail);
    Assert.True(viewModel.CanLoadMoreTransparency);
    Assert.Null(viewModel.TransparencyPaginationError);
  }

  [Fact]
  public async Task NewerRangeReloadWinsWhenInitialRequestsCompleteOutOfOrder()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "member"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    var olderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var olderResponse = new TaskCompletionSource<ModerationTransparencyResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.ModerationTransparencyHandler = (_, range, _, _) =>
    {
      if (range == "all")
      {
        olderStarted.SetResult();
        return olderResponse.Task;
      }
      return Task.FromResult(new ModerationTransparencyResponse(
          range,
          [new ModerationTransparencyBucket("2026-08-14", "reports", "harassment", 25)]));
    };

    var olderLoad = viewModel.SelectModerationTransparencyRangeAsync(
        "all",
        TestContext.Current.CancellationToken);
    await olderStarted.Task;
    await viewModel.SelectModerationTransparencyRangeAsync("7d", TestContext.Current.CancellationToken);
    olderResponse.SetResult(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2025-08-01", "reports", "spam", 30)],
        "older-page"));
    await olderLoad;

    Assert.Equal("7d", viewModel.ModerationTransparencyRange);
    Assert.Single(viewModel.Moderation);
    Assert.Contains("Harassment", viewModel.Moderation.Single().Detail);
    Assert.False(viewModel.CanLoadMoreTransparency);
  }

  [Fact]
  public async Task SiteModeratorLoadsRawAnalyticsWithoutCommunityMembership()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var session = new SessionSnapshot(new User(
        Id: "moderator-1",
        Username: "moderator",
        Roles: ["moderator"],
        EmailAddress: "moderator@example.com",
        MembershipPlan: "free"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(session));

    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanModerateCommunity);
    Assert.True(viewModel.CanViewRawModerationAnalytics);
    Assert.Equal(1, service.ModerationAnalyticsFetchCount);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Contains(viewModel.Moderation, row => row.Id == "queue-volume");
  }

  [Fact]
  public async Task RawCommunityAnalyticsSurvivesSupplementaryTransparencyFailures()
  {
    var service = new ScriptedCommunitiesService
    {
      ModerationTransparencyFailure = new HttpRequestException("unavailable"),
    };
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var session = new SessionSnapshot(new User(
        Id: "moderator-1", Username: "moderator", Roles: ["moderator"],
        EmailAddress: "moderator@example.com", MembershipPlan: "free"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(session));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Contains(viewModel.Moderation, row => row.Id == "queue-volume");
  }

  [Fact]
  public async Task RawAnalyticsViewerCanSelectAndContinueAllTimeTransparency()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyHandler = (_, range, after, _) => Task.FromResult(
        after is null
            ? new ModerationTransparencyResponse(
                range,
                [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
                "older-page")
            : new ModerationTransparencyResponse(
                range,
                [new ModerationTransparencyBucket("2026-07-01", "reports", "harassment", 10)]));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "owner"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);
    await viewModel.LoadMoreTransparencyAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanViewRawModerationAnalytics);
    Assert.Contains(viewModel.Moderation, row => row.Id == "queue-volume");
    Assert.Contains(viewModel.Moderation, row => row.Id == "transparency-2026-08-01-reports-spam");
    Assert.Contains(viewModel.Moderation, row => row.Id == "transparency-2026-07-01-reports-harassment");
    Assert.False(viewModel.CanLoadMoreTransparency);
  }

  [Theory]
  [InlineData("today")]
  [InlineData("all")]
  public async Task RawAnalyticsAndTransparencyUseTheSelectedRange(string range)
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "owner"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));

    viewModel.SetModerationTransparencyRange(range);
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.ModerationAnalytics,
        TestContext.Current.CancellationToken);

    Assert.Equal(range, service.ModerationAnalyticsRange);
    Assert.Equal(range, service.ModerationTransparencyRange);
  }
}

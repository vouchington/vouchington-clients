using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Support;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityModerationAnalyticsDenialTests
{
  [Fact]
  public async Task RevokedRawAnalyticsAccessReplacesPreviousRowsAndCursorWithLockedState()
  {
    var service = new ScriptedCommunitiesService();
    service.ModerationTransparencyResponses.Enqueue(new ModerationTransparencyResponse(
        "all",
        [new ModerationTransparencyBucket("2026-08-01", "reports", "spam", 20)],
        "older-page"));
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "owner"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    viewModel.SetModerationTransparencyRange("all");
    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);
    Assert.Contains(viewModel.Moderation, row => row.Id == "queue-volume");
    Assert.True(viewModel.CanLoadMoreTransparency);

    service.ModerationAnalyticsFailure = new VouchaApiException(HttpStatusCode.Forbidden, "{}");
    await viewModel.SelectModerationTransparencyRangeAsync("7d", TestContext.Current.CancellationToken);

    Assert.Equal("7d", viewModel.ModerationTransparencyRange);
    Assert.Equal("transparency-locked", viewModel.Moderation.Single().Id);
    Assert.False(viewModel.CanLoadMoreTransparency);
    Assert.Null(viewModel.TransparencyPaginationError);
  }

  [Fact]
  public async Task HandledRawAnalyticsDenialRestoresLoadedStateAfterGenericFailure()
  {
    var service = new ScriptedCommunitiesService
    {
      ModerationAnalyticsFailure = new HttpRequestException("unavailable"),
    };
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "owner"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);
    Assert.Equal(LoadState.Error, viewModel.State);

    service.ModerationAnalyticsFailure = new VouchaApiException(HttpStatusCode.Forbidden, "{}");
    await viewModel.LoadSelectedSectionAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal("transparency-locked", viewModel.Moderation.Single().Id);
  }

  [Theory]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.NotFound)]
  public async Task StaleRawAnalyticsDenialCannotReplaceNewerTransparencyRange(HttpStatusCode statusCode)
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false, membershipRole: "owner"));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var delayed = new TaskCompletionSource<CommunityModerationAnalyticsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.ModerationAnalyticsHandler = (_, range, _) =>
    {
      if (range == "all")
      {
        started.SetResult();
        return delayed.Task;
      }
      throw new InvalidOperationException("Only the older all-time request should use the delayed handler.");
    };

    var olderLoad = viewModel.SelectModerationTransparencyRangeAsync("all", TestContext.Current.CancellationToken);
    await started.Task;
    service.ModerationAnalyticsHandler = null;
    await viewModel.SelectModerationTransparencyRangeAsync("7d", TestContext.Current.CancellationToken);
    delayed.SetException(new VouchaApiException(statusCode, "{}"));
    await olderLoad;

    Assert.Equal("7d", viewModel.ModerationTransparencyRange);
    Assert.Contains(viewModel.Moderation, row => row.Id == "queue-volume");
    Assert.DoesNotContain(viewModel.Moderation, row => row.Id == "transparency-locked");
  }

  [Theory]
  [InlineData(HttpStatusCode.Forbidden, true)]
  [InlineData(HttpStatusCode.NotFound, false)]
  public async Task InitialStaffTransparencyDenialPreservesRawRowsOnlyForForbidden(
      HttpStatusCode statusCode,
      bool preservesRawRows)
  {
    var service = new ScriptedCommunitiesService
    {
      ModerationTransparencyFailure = new VouchaApiException(statusCode, "{}"),
    };
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: false, hasPendingApplication: false));
    var session = new SessionSnapshot(new User(
        Id: "moderator-1", Username: "moderator", Roles: ["moderator"],
        EmailAddress: "moderator@example.com", MembershipPlan: "free"));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(session));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModerationAnalytics, TestContext.Current.CancellationToken);

    Assert.Equal(preservesRawRows, viewModel.Moderation.Any(row => row.Id == "queue-volume"));
    Assert.Contains(viewModel.Moderation, row => row.Id == "transparency-locked");
  }
}

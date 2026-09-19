using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityModerationResultsRaceTests
{
  [Fact]
  public async Task LateEarlierPostResultCannotOverwriteCurrentModerationSurface()
  {
    var staleResponse = new TaskCompletionSource<CommunityModerationResultsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ScriptedCommunitiesService
    {
      ModerationResultsHandler = (_, postId, _) => postId == "stale-post"
          ? staleResponse.Task
          : Task.FromResult(Response(AdminReviewQueueClearanceStatus.Approved)),
    };
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    var staleLoad = viewModel.LoadModerationResultsAsync(
        "stale-post",
        TestContext.Current.CancellationToken);
    await viewModel.LoadModerationResultsAsync(
        "current-post",
        TestContext.Current.CancellationToken);
    staleResponse.SetResult(Response(AdminReviewQueueClearanceStatus.Rejected));
    await staleLoad;

    Assert.Equal(
        UiMessageKey.NativeDotnetModerationApproved,
        viewModel.Moderation.Single(row => row.Id == "platform-moderation").SubtitleText.Key);
  }

  [Fact]
  public async Task NavigationAwayAndBackRejectsLateModerationResult()
  {
    var staleResponse = new TaskCompletionSource<CommunityModerationResultsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ScriptedCommunitiesService
    {
      ModerationResultsHandler = (_, _, _) => staleResponse.Task,
    };
    SeedLoadResponseSet(
        service,
        CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.Settings,
        TestContext.Current.CancellationToken);

    var staleLoad = viewModel.LoadModerationResultsAsync(
        "stale-post",
        TestContext.Current.CancellationToken);
    await viewModel.SelectSectionAsync(
        CommunityDetailSurfaceSection.Overview,
        TestContext.Current.CancellationToken);
    await viewModel.SelectSectionAsync(
        CommunityDetailSurfaceSection.Settings,
        TestContext.Current.CancellationToken);
    staleResponse.SetResult(Response(AdminReviewQueueClearanceStatus.Rejected));
    await staleLoad;

    Assert.DoesNotContain(viewModel.Moderation, row => row.Id == "platform-moderation");
    Assert.Contains(viewModel.Moderation, row => row.Id == "saved-reply-1");
  }

  private static CommunityModerationResultsResponse Response(
      AdminReviewQueueClearanceStatus status) =>
      new([], new CommunityPlatformModeration(status));
}

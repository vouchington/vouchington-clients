using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationDisputesViewModelReconciliationTests
{
  [Fact]
  public async Task AmbiguousApprovalRefreshPreservesAnUnconfirmedLocalDraft()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();
    viewModel.SetPublicResponseDraft(dispute, "Locally edited response");
    service.FailNext(ModerationDisputesTestService.AmbiguousFailure());

    await viewModel.ApproveAsync(dispute, TestContext.Current.CancellationToken);
    service.Refreshes.Enqueue(service.Current with { PublicResponse = "Older server response" });
    await viewModel.RefreshAmbiguousAsync(
        dispute.Id,
        TestContext.Current.CancellationToken);

    var refreshed = viewModel.Disputes.Single();
    Assert.Equal("Locally edited response", viewModel.PublicResponseDraftFor(refreshed));
    Assert.False(viewModel.IsMutationAmbiguous(refreshed));
  }

  [Fact]
  public async Task SuccessfulRerunPollingPreservesAnEditedPublicResponse()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();
    viewModel.SetPublicResponseDraft(dispute, "Locally edited response");
    service.Refreshes.Enqueue(dispute with
    {
      AiPublicResponse = "New AI response",
      AiDraftedAt = (dispute.AiDraftedAt ?? DateTimeOffset.UtcNow).AddMinutes(1),
      LatestLifecycleChangeId = "rerun-complete",
    });

    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);

    var refreshed = viewModel.Disputes.Single();
    Assert.Equal("New AI response", refreshed.AiPublicResponse);
    Assert.Equal("Locally edited response", viewModel.PublicResponseDraftFor(refreshed));
    Assert.True(viewModel.CanApprove(refreshed));
  }

  [Fact]
  public async Task ResolutionRejectsAnOlderInFlightListSnapshot()
  {
    var service = new ModerationDisputesTestService
    {
      Current = ModerationDisputesTestService.Fixture() with
      {
        ApprovedAt = DateTimeOffset.UtcNow,
        SentAt = DateTimeOffset.UtcNow,
      },
    };
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var pending = viewModel.Disputes.Single();
    var stalePage = new TaskCompletionSource<ModerationDisputeListResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingFetch = stalePage;

    var load = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await service.PendingFetchStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.ResolveAsync(
        pending,
        ModerationDisputeResolutionAction.Remove,
        TestContext.Current.CancellationToken);
    stalePage.SetResult(new([pending], new PageInfo(null, false, null)));
    await load;

    Assert.Empty(viewModel.Disputes);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task ResolutionOutcomeRejectsARefreshStartedDuringTheMutation()
  {
    var stalePage = new TaskCompletionSource<ModerationDisputeListResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var pendingResolution = new TaskCompletionSource<ModerationDisputeResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new ModerationDisputesTestService
    {
      Current = ModerationDisputesTestService.Fixture() with
      {
        ApprovedAt = DateTimeOffset.UtcNow,
        SentAt = DateTimeOffset.UtcNow,
      },
      PendingResolve = pendingResolution,
    };
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var pending = viewModel.Disputes.Single();
    service.PendingFetch = stalePage;

    var resolution = viewModel.ResolveAsync(
        pending,
        ModerationDisputeResolutionAction.Remove,
        TestContext.Current.CancellationToken);
    await service.ResolveStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    var refresh = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await service.PendingFetchStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    var resolved = service.Current with { Status = ModerationDisputeStatus.Resolved };
    pendingResolution.SetResult(new(resolved));
    await resolution;
    stalePage.SetResult(new([pending], new PageInfo(null, false, null)));
    await refresh;

    Assert.Empty(viewModel.Disputes);
    Assert.False(viewModel.IsLoading);
  }

  private static ModerationDisputesViewModel Staff(ModerationDisputesTestService service) =>
      new(service, new NavigationViewer(true, ["moderator"]));
}

using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ModerationAppealsViewModelTests
{
  [Fact]
  public async Task RerunRefreshesTheAppealAndPreservesServerConfirmation()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.Details.Enqueue(service.Current);
    service.Details.Enqueue(service.Current with
    {
      AiPublicResponse = "New AI draft",
      AiDraftedAt = service.Current.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
    });

    await viewModel.RerunAsync(viewModel.Appeals.Single(), TestContext.Current.CancellationToken);

    Assert.Equal("New AI draft", viewModel.Appeals.Single().AiPublicResponse);
    var id = service.Current.Id;
    Assert.Equal([$"rerun:{id}", $"detail:{id}", $"detail:{id}"], service.Calls.Skip(1));
  }

  [Fact]
  public async Task RerunTimeoutPreservesTheExistingRowAndDirtyDraft()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 2);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    viewModel.SetDraft(appeal, "Local edit");

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.Equal(appeal.AiPublicResponse, viewModel.Appeals.Single().AiPublicResponse);
    Assert.Equal("Local edit", viewModel.DraftFor(viewModel.Appeals.Single()));
    Assert.Equal("The AI draft is still processing. Refresh to check again.", viewModel.ErrorMessage);
    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.False(viewModel.CanRerun(appeal));
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task PendingRerunRetriesOnlyTheTargetedReadAndRetainsDirtyDraft()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    viewModel.SetDraft(appeal, "Local edit");
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    service.NextError = new HttpRequestException("read failed");
    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.Equal("Local edit", viewModel.DraftFor(appeal));

    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));
    service.Details.Enqueue(appeal with
    {
      AiPublicResponse = "Finished",
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = appeal.LatestLifecycleChangeId,
    });
    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));

    service.Details.Enqueue(appeal with
    {
      AiPublicResponse = "Finished",
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
    });
    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.Equal("Finished", viewModel.Appeals.Single().AiPublicResponse);
    Assert.Equal("Local edit", viewModel.DraftFor(viewModel.Appeals.Single()));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task ListReloadKeepsPendingUntilBothResultMarkersAdvance()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    service.Current = appeal with
    {
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
    };
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.False(viewModel.CanRerun(viewModel.Appeals.Single()));

    service.Current = service.Current with { LatestLifecycleChangeId = "new-change" };
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.True(viewModel.CanRerun(viewModel.Appeals.Single()));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task NegativeAdmissionReleasesRerunButLostResponseRequiresReadReconciliation()
  {
    var rejected = new ModerationAppealsTestService { QueueAccepted = false };
    var rejectedViewModel = PollingStaffViewModel(rejected, attempts: 1);
    await rejectedViewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = rejectedViewModel.Appeals.Single();
    await rejectedViewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.False(rejectedViewModel.IsAIRerunPending(appeal));
    Assert.True(rejectedViewModel.CanRerun(appeal));

    var ambiguous = new ModerationAppealsTestService();
    var ambiguousViewModel = PollingStaffViewModel(ambiguous, attempts: 1);
    await ambiguousViewModel.LoadAsync(TestContext.Current.CancellationToken);
    appeal = ambiguousViewModel.Appeals.Single();
    ambiguous.NextError = new HttpRequestException("response lost");
    await ambiguousViewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(ambiguousViewModel.IsAIRerunPending(appeal));
    Assert.False(ambiguousViewModel.CanRerun(appeal));
    await ambiguousViewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.Single(ambiguous.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task DefiniteClientRejectionReleasesPendingAdmission()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.NextError = new HttpRequestException("rejected", null, HttpStatusCode.BadRequest);

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.True(viewModel.CanRerun(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task RequestTimeoutResponseKeepsPendingAdmission()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.NextError = new HttpRequestException("timed out", null, HttpStatusCode.RequestTimeout);

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.False(viewModel.CanRerun(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task AccountChangeRejectsHeldOldOwnerResultAndClearsPendingState()
  {
    var service = new ModerationAppealsTestService();
    var owner = new NavigationViewer(true, ["administrator"], IdentityId: "owner-a");
    var provider = new MutableNavigationViewerProvider();
    provider.SetViewer(owner);
    using var viewModel = new ModerationAppealsViewModel(
        service, owner, 1, _ => Task.CompletedTask, viewerProvider: provider);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    var held = new TaskCompletionSource<ModerationAppealResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingDetail = held;
    var rerun = viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));

    provider.SetViewer(new NavigationViewer(true, ["administrator"], IdentityId: "owner-b"));
    held.SetResult(new ModerationAppealResponse(appeal with
    {
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
    }));
    await rerun;

    Assert.False(viewModel.CanAccess);
    Assert.Empty(viewModel.Appeals);
    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task AccountChangeRejectsHeldOldOwnerList()
  {
    var service = new ModerationAppealsTestService();
    var owner = new NavigationViewer(true, ["administrator"], IdentityId: "owner-a");
    var provider = new MutableNavigationViewerProvider();
    provider.SetViewer(owner);
    using var viewModel = new ModerationAppealsViewModel(
        service, owner, 1, _ => Task.CompletedTask, viewerProvider: provider);
    var held = new TaskCompletionSource<ModerationAppealListResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingFetch = held;
    var load = viewModel.LoadAsync(TestContext.Current.CancellationToken);

    provider.SetViewer(new NavigationViewer(true, ["administrator"], IdentityId: "owner-b"));
    held.SetResult(new ModerationAppealListResponse(
        [service.Current], new PageInfo(null, false, null)));
    await load;

    Assert.Empty(viewModel.Appeals);
    Assert.False(viewModel.CanAccess);
  }

  [Fact]
  public async Task StatusSwitchRejectsHeldReadForNoLongerSelectedAppeal()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    var held = new TaskCompletionSource<ModerationAppealResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingDetail = held;
    var refresh = viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    service.Current = service.Appeal("other", ModerationAppealStatus.Resolved);
    await viewModel.SelectStatusAsync(ModerationAppealStatus.Resolved, TestContext.Current.CancellationToken);
    held.SetResult(new ModerationAppealResponse(appeal with
    {
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
    }));
    await refresh;

    Assert.Equal("other", viewModel.Appeals.Single().Id);
    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task RerunCancellationStopsPollingAndPreservesTheExistingRow()
  {
    var service = new ModerationAppealsTestService();
    var delayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var viewModel = new ModerationAppealsViewModel(
        service,
        new NavigationViewer(true, ["administrator"]),
        20,
        async token =>
        {
          delayStarted.SetResult();
          await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var original = viewModel.Appeals.Single();
    using var cancellation = new CancellationTokenSource();
    var rerun = viewModel.RerunAsync(original, cancellation.Token);
    await delayStarted.Task;

    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rerun);
    Assert.Equal(original.AiPublicResponse, viewModel.Appeals.Single().AiPublicResponse);
    Assert.Single(service.Calls, call => call == $"detail:{original.Id}");
    Assert.False(viewModel.IsMutating);
  }

  [Theory]
  [InlineData(false, true)]
  [InlineData(true, false)]
  public async Task ApprovedOrSentAppealsCannotRerunResolutionDrafts(bool approved, bool sent)
  {
    var service = new ModerationAppealsTestService
    {
      Current = new ModerationAppealsTestService().Appeal(approved: approved) with
      {
        SentAt = sent ? DateTimeOffset.UtcNow : null,
      },
    };
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();

    Assert.False(viewModel.CanRerun(appeal));
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(service.Calls, call => call.StartsWith("rerun:", StringComparison.Ordinal));
  }

  private static ModerationAppealsViewModel PollingStaffViewModel(
      ModerationAppealsTestService service,
      int attempts = 20) =>
      new(service, new NavigationViewer(true, ["administrator"]), attempts, _ => Task.CompletedTask);
}

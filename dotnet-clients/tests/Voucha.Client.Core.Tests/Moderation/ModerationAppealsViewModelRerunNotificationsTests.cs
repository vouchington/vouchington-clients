using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ModerationAppealsViewModelTests
{
  [Fact]
  public async Task AlreadyCanceledRerunDoesNotAdmitOrSend()
  {
    var service = new ModerationAppealsTestService();
    using var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => viewModel.RerunAsync(appeal, cancellation.Token));

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.True(viewModel.CanRerun(appeal));
    Assert.DoesNotContain(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task OldPollClearingAdmissionNotifiesReopenedOwnerOnly()
  {
    var service = new ModerationAppealsTestService();
    var store = new AppealAIRerunAdmissionStore();
    var owner = new NavigationViewer(true, ["administrator"], IdentityId: "staff-1");
    var first = new ModerationAppealsViewModel(
        service, owner, 1, _ => Task.CompletedTask,
        admissionStore: store, apiBaseUrl: new Uri("https://example.test/api"));
    await first.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = first.Appeals.Single();
    var held = new TaskCompletionSource<ModerationAppealResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingDetail = held;
    var rerun = first.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(first.IsAIRerunPending(appeal));
    first.Dispose();

    using var reopened = new ModerationAppealsViewModel(
        service, owner, 1, _ => Task.CompletedTask,
        admissionStore: store, apiBaseUrl: new Uri("https://EXAMPLE.test:443/api/"));
    await reopened.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(reopened.CanRerun(reopened.Appeals.Single()));
    var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    reopened.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ModerationAppealsViewModel.Appeals)) changed.TrySetResult();
    };

    using var otherApi = new ModerationAppealsViewModel(
        service, owner, 1, _ => Task.CompletedTask,
        admissionStore: store, apiBaseUrl: new Uri("https://example.test/other"));
    await otherApi.LoadAsync(TestContext.Current.CancellationToken);
    var otherOwnerNotifications = 0;
    otherApi.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ModerationAppealsViewModel.Appeals)) otherOwnerNotifications++;
    };

    held.SetResult(new ModerationAppealResponse(appeal with
    {
      AiDraftedAt = appeal.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
    }));
    await rerun;
    await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.False(reopened.IsAIRerunPending(appeal));
    Assert.True(reopened.CanRerun(reopened.Appeals.Single()));
    Assert.Equal(0, otherOwnerNotifications);
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }
}

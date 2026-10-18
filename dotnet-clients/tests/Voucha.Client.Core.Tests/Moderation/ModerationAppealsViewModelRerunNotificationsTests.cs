using System.Collections.Concurrent;
using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ModerationAppealsViewModelTests
{
  [Fact]
  public async Task SuccessfulPostWithUndecodableResponseKeepsAdmissionUntilRead()
  {
    var service = new ModerationAppealsTestService();
    using var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.NextError = new VouchaApiDecodeException(HttpStatusCode.Accepted, "invalid queue response");

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.False(viewModel.CanRerun(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

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

  [Fact]
  public async Task BackgroundViewerChangeFencesWorkBeforeMarshallingBoundState()
  {
    var service = new ModerationAppealsTestService();
    var provider = new MutableNavigationViewerProvider();
    var owner = new NavigationViewer(true, ["administrator"], IdentityId: "staff-1");
    provider.SetViewer(owner);
    var dispatcher = new QueuedAppealDispatcher();
    var viewModel = new ModerationAppealsViewModel(
        service, owner, viewerProvider: provider, uiDispatcher: dispatcher);
    using (viewModel)
    {
      await viewModel.LoadAsync(TestContext.Current.CancellationToken);
      Assert.Single(viewModel.Appeals);
      var notifications = 0;
      viewModel.PropertyChanged += (_, args) =>
      {
        if (args.PropertyName == nameof(ModerationAppealsViewModel.Appeals))
        {
          Assert.True(dispatcher.IsDraining);
          notifications++;
        }
      };
      var originalAppealId = viewModel.Appeals.Single().Id;
      var heldLoad = new TaskCompletionSource<ModerationAppealListResponse>(
          TaskCreationOptions.RunContinuationsAsynchronously);
      service.PendingFetch = heldLoad;
      var reload = viewModel.LoadAsync(TestContext.Current.CancellationToken);

      await Task.Run(() => provider.SetViewer(NavigationViewer.Anonymous), TestContext.Current.CancellationToken);
      heldLoad.SetResult(new ModerationAppealListResponse(
          [service.Appeal("stale-appeal")], new PageInfo(null, false, null)));
      await reload;
      Assert.False(viewModel.CanAccess);
      Assert.Equal(originalAppealId, viewModel.Appeals.Single().Id);
      Assert.Equal(0, notifications);
      dispatcher.Drain();
      Assert.Empty(viewModel.Appeals);
      Assert.True(notifications > 0);
      var notificationsAfterReset = notifications;

      await Task.Run(
          () => provider.SetViewer(new NavigationViewer(true, ["administrator"], IdentityId: "staff-2")),
          TestContext.Current.CancellationToken);
      viewModel.Dispose();
      dispatcher.Drain();
      Assert.Equal(notificationsAfterReset, notifications);
    }
  }

  private sealed class QueuedAppealDispatcher : IUiThreadDispatcher
  {
    private readonly ConcurrentQueue<Action> queued = new();

    public bool IsDispatchRequired => true;
    public bool IsDraining { get; private set; }
    public void Dispatch(Action action) => queued.Enqueue(action);

    public void Drain()
    {
      IsDraining = true;
      try
      {
        while (queued.TryDequeue(out var action)) action();
      }
      finally
      {
        IsDraining = false;
      }
    }
  }
}

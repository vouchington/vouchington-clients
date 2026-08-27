using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ModerationAppealsViewModelTests
{
  [Fact]
  public async Task DefaultsToPendingAndRejectsUnauthorizedViewers()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = new ModerationAppealsViewModel(service, NavigationViewer.Anonymous);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ModerationAppealStatus.Pending, viewModel.SelectedStatus);
    Assert.False(viewModel.CanAccess);
    Assert.Empty(service.Calls);
  }

  [Fact]
  public async Task AuthenticatedMemberCannotAccessOrLoadStaffAppeals()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = new ModerationAppealsViewModel(service, new NavigationViewer(true, ["member"]));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsSignedIn);
    Assert.False(viewModel.CanAccess);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.Empty(viewModel.Appeals);
    Assert.Empty(service.Calls);
  }

  [Fact]
  public async Task LoadAndPaginationDeduplicateAndRejectStaleResults()
  {
    var service = new ModerationAppealsTestService();
    var first = service.Appeal("appeal-1");
    var second = service.Appeal("appeal-2");
    service.Pages.Enqueue(new([first], new PageInfo("opaque-appeal-cursor", true, null)));
    service.Pages.Enqueue(new([first, second], new PageInfo(null, false, null)));
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["appeal-1", "appeal-2"], viewModel.Appeals.Select(appeal => appeal.Id));

    var pending = new TaskCompletionSource<ModerationAppealListResponse>();
    service.PendingFetch = pending;
    var staleLoad = viewModel.SelectStatusAsync(ModerationAppealStatus.Dismissed, TestContext.Current.CancellationToken);
    service.Current = service.Appeal("appeal-resolved", ModerationAppealStatus.Resolved);
    var currentLoad = viewModel.SelectStatusAsync(ModerationAppealStatus.Resolved, TestContext.Current.CancellationToken);
    pending.SetResult(new([service.Appeal("appeal-stale", ModerationAppealStatus.Dismissed)], new PageInfo(null, false, null)));
    await Task.WhenAll(staleLoad, currentLoad);

    Assert.Equal(ModerationAppealStatus.Resolved, viewModel.SelectedStatus);
    Assert.Equal("appeal-resolved", viewModel.Appeals.Single().Id);
  }

  [Fact]
  public async Task UnexpectedStaleLoadFailureDoesNotReplaceCurrentState()
  {
    var service = new ModerationAppealsTestService();
    var pending = new TaskCompletionSource<ModerationAppealListResponse>();
    service.PendingFetch = pending;
    var viewModel = StaffViewModel(service);
    var staleLoad = viewModel.SelectStatusAsync(
        ModerationAppealStatus.Dismissed,
        TestContext.Current.CancellationToken);
    service.Current = service.Appeal("appeal-resolved", ModerationAppealStatus.Resolved);

    await viewModel.SelectStatusAsync(ModerationAppealStatus.Resolved, TestContext.Current.CancellationToken);
    pending.SetException(new NotSupportedException("unexpected stale failure"));
    await staleLoad;

    Assert.Equal(ModerationAppealStatus.Resolved, viewModel.SelectedStatus);
    Assert.Equal("appeal-resolved", viewModel.Appeals.Single().Id);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task LoadCancellationDistinguishesCallerIntentFromTransportFailure()
  {
    var callerService = new ModerationAppealsTestService();
    var callerPending = new TaskCompletionSource<ModerationAppealListResponse>();
    callerService.PendingFetch = callerPending;
    var callerViewModel = StaffViewModel(callerService);
    using var cancellation = new CancellationTokenSource();
    var callerLoad = callerViewModel.LoadAsync(cancellation.Token);
    cancellation.Cancel();
    callerPending.SetCanceled(cancellation.Token);

    await callerLoad;

    Assert.Equal(LoadState.Idle, callerViewModel.State);
    Assert.Null(callerViewModel.ErrorMessage);

    var transportService = new ModerationAppealsTestService
    {
      NextError = new TaskCanceledException("transport timeout"),
    };
    var transportViewModel = StaffViewModel(transportService);

    await transportViewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, transportViewModel.State);
    Assert.Equal("transport timeout", transportViewModel.ErrorMessage);

    var paginationService = new ModerationAppealsTestService();
    paginationService.Pages.Enqueue(new([paginationService.Current], new PageInfo("next", true, null)));
    var paginationViewModel = StaffViewModel(paginationService);
    await paginationViewModel.LoadAsync(TestContext.Current.CancellationToken);
    paginationService.NextError = new TaskCanceledException("pagination transport timeout");

    await paginationViewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Single(paginationViewModel.Appeals);
    Assert.Equal(LoadState.Loaded, paginationViewModel.State);
    Assert.Equal("pagination transport timeout", paginationViewModel.ErrorMessage);
  }

  [Fact]
  public async Task StaleTransportCancellationCannotReplaceCurrentLoadState()
  {
    var service = new ModerationAppealsTestService();
    var pending = new TaskCompletionSource<ModerationAppealListResponse>();
    service.PendingFetch = pending;
    var viewModel = StaffViewModel(service);
    var staleLoad = viewModel.SelectStatusAsync(
        ModerationAppealStatus.Dismissed,
        TestContext.Current.CancellationToken);
    service.Current = service.Appeal("appeal-resolved", ModerationAppealStatus.Resolved);

    await viewModel.SelectStatusAsync(ModerationAppealStatus.Resolved, TestContext.Current.CancellationToken);
    pending.SetException(new TaskCanceledException("stale transport timeout"));
    await staleLoad;

    Assert.Equal("appeal-resolved", viewModel.Appeals.Single().Id);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task DraftSaveRetainsFailedEditsAndSkipsNoOpRequests()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    viewModel.SetDraft(appeal, "Edited response");
    service.NextError = new HttpRequestException("offline");

    Assert.False(await viewModel.SaveDraftAsync(appeal, TestContext.Current.CancellationToken));
    Assert.Equal("Edited response", viewModel.DraftFor(appeal));
    viewModel.SetDraft(appeal, appeal.PublicResponse ?? string.Empty);
    Assert.True(await viewModel.SaveDraftAsync(appeal, TestContext.Current.CancellationToken));
    Assert.Single(service.Calls, call => call.StartsWith("update:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task ReloadReseedsCleanDraftsPreservesDirtyDraftsAndClearsDirtyAfterSave()
  {
    var service = new ModerationAppealsTestService
    {
      Current = new ModerationAppealsTestService().Appeal() with { AiPublicResponse = "Initial AI draft" },
    };
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.Current = service.Current with { AiPublicResponse = "Fresh AI draft" };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var appeal = viewModel.Appeals.Single();
    Assert.Equal("Fresh AI draft", viewModel.DraftFor(appeal));
    viewModel.SetDraft(appeal, "Local moderator draft");
    service.Current = service.Current with { PublicResponse = "Server reload draft" };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    appeal = viewModel.Appeals.Single();
    Assert.Equal("Local moderator draft", viewModel.DraftFor(appeal));
    var pendingAppeal = service.Current;
    service.Current = service.Appeal("appeal-resolved", ModerationAppealStatus.Resolved);
    await viewModel.SelectStatusAsync(ModerationAppealStatus.Resolved, TestContext.Current.CancellationToken);
    service.Current = pendingAppeal with { PublicResponse = "Server draft after status switch" };
    await viewModel.SelectStatusAsync(ModerationAppealStatus.Pending, TestContext.Current.CancellationToken);
    appeal = viewModel.Appeals.Single();
    Assert.Equal("Local moderator draft", viewModel.DraftFor(appeal));
    Assert.True(await viewModel.SaveDraftAsync(appeal, TestContext.Current.CancellationToken));
    service.Current = service.Current with { PublicResponse = "Fresh after save" };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Fresh after save", viewModel.DraftFor(viewModel.Appeals.Single()));
  }

  [Fact]
  public async Task NoOpSaveConfirmsConvergedDraftAndAllowsLaterServerRefresh()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    viewModel.SetDraft(appeal, "Converged response");
    service.Current = service.Current with { PublicResponse = "Converged response" };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    appeal = viewModel.Appeals.Single();
    Assert.True(await viewModel.SaveDraftAsync(appeal, TestContext.Current.CancellationToken));
    Assert.DoesNotContain(service.Calls, call => call.StartsWith("update:", StringComparison.Ordinal));
    service.Current = service.Current with { PublicResponse = "Later server response" };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Later server response", viewModel.DraftFor(viewModel.Appeals.Single()));
  }

  [Fact]
  public async Task UnexpectedLoadAndMutationFailuresAreContainedInViewModelState()
  {
    var loadService = new ModerationAppealsTestService
    {
      NextError = new NotSupportedException("unexpected load failure"),
    };
    var loadViewModel = StaffViewModel(loadService);

    await loadViewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Error, loadViewModel.State);
    Assert.Equal("unexpected load failure", loadViewModel.ErrorMessage);

    var mutationService = new ModerationAppealsTestService();
    var mutationViewModel = StaffViewModel(mutationService);
    await mutationViewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = mutationViewModel.Appeals.Single();
    mutationViewModel.SetDraft(appeal, "Edited response");
    mutationService.NextError = new NotSupportedException("unexpected mutation failure");

    Assert.False(await mutationViewModel.SaveDraftAsync(appeal, TestContext.Current.CancellationToken));
    Assert.Equal("unexpected mutation failure", mutationViewModel.ErrorMessage);
    Assert.False(mutationViewModel.IsMutating);
  }

  [Fact]
  public async Task ApprovalSavesBeforeApprovingAndReplacesServerState()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    viewModel.SetDraft(appeal, "Approved response");

    await viewModel.ApproveAsync(appeal, TestContext.Current.CancellationToken);

    Assert.Equal(
        [$"update:{appeal.Id}:Approved response", $"approve:{appeal.Id}"],
        service.Calls.Skip(1));
    Assert.NotNull(viewModel.Appeals.Single().ApprovedAt);
  }

  [Fact]
  public async Task MutationReplacementPreservesExistingRowOrder()
  {
    var service = new ModerationAppealsTestService();
    var first = service.Appeal("appeal-1");
    var second = service.Appeal("appeal-2");
    service.Pages.Enqueue(new([first, second], new PageInfo(null, false, null)));
    service.Current = second;
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.SetDraft(second, "Updated second response");

    Assert.True(await viewModel.SaveDraftAsync(second, TestContext.Current.CancellationToken));
    Assert.Equal(["appeal-1", "appeal-2"], viewModel.Appeals.Select(appeal => appeal.Id));
  }

  [Fact]
  public async Task DeliveryGuardsAmbiguousFailuresAndGlobalMutationOverlap()
  {
    var service = new ModerationAppealsTestService();
    service.Current = service.Appeal(response: "Approved", approved: true);
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.PendingDelivery = new();
    var delivery = viewModel.DeliverAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsMutating);
    Assert.False(await viewModel.SaveDraftAsync(appeal, TestContext.Current.CancellationToken));
    service.NextError = new NotSupportedException("unexpected confirmation failure");
    service.PendingDelivery.SetException(new HttpRequestException("connection lost"));
    await delivery;
    Assert.True(viewModel.IsDeliveryAmbiguous(appeal));
    Assert.False(viewModel.CanDeliver(appeal));
    Assert.Equal("unexpected confirmation failure", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ApprovedAppealCannotDeliverAnUnsavedLocalDraft()
  {
    var service = new ModerationAppealsTestService
    {
      Current = new ModerationAppealsTestService().Appeal(response: "Server-confirmed response", approved: true),
    };
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();

    Assert.True(viewModel.CanDeliver(appeal));
    viewModel.SetDraft(appeal, "Unsaved local edit");
    Assert.False(viewModel.CanDeliver(appeal));
    viewModel.SetDraft(appeal, appeal.PublicResponse!);
    Assert.True(viewModel.CanDeliver(appeal));
  }

  [Fact]
  public async Task SuccessfulEmptyLoadIsDistinctFromLoading()
  {
    var service = new ModerationAppealsTestService();
    var pending = new TaskCompletionSource<ModerationAppealListResponse>();
    service.PendingFetch = pending;
    var viewModel = StaffViewModel(service);

    var load = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsLoading);
    Assert.False(viewModel.IsEmpty);
    pending.SetResult(new([], new PageInfo(null, false, null)));
    await load;

    Assert.False(viewModel.IsLoading);
    Assert.True(viewModel.IsEmpty);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task DeliveryConfirmsTransportTimeoutsAndHttpRequestTimeouts()
  {
    Exception[] failures =
    [
      new TaskCanceledException("transport timeout"),
      new HttpRequestException("request timeout", null, HttpStatusCode.RequestTimeout),
    ];
    foreach (var failure in failures)
    {
      var service = new ModerationAppealsTestService { Current = new ModerationAppealsTestService().Appeal(response: "Approved", approved: true) };
      var viewModel = StaffViewModel(service);
      await viewModel.LoadAsync(TestContext.Current.CancellationToken);
      var appeal = viewModel.Appeals.Single();
      service.PendingDelivery = new();
      var delivery = viewModel.DeliverAsync(appeal, TestContext.Current.CancellationToken);
      service.Current = service.Current with { SentAt = DateTimeOffset.UtcNow };
      service.PendingDelivery.SetException(failure);

      await delivery;

      Assert.NotNull(viewModel.Appeals.Single().SentAt);
      Assert.False(viewModel.IsDeliveryAmbiguous(viewModel.Appeals.Single()));
      Assert.Contains("detail:appeal-1", service.Calls);
    }
  }

  [Fact]
  public async Task DeliveryPropagatesCallerCancellationWithoutMarkingAmbiguous()
  {
    var service = new ModerationAppealsTestService { Current = new ModerationAppealsTestService().Appeal(response: "Approved", approved: true) };
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.PendingDelivery = new();
    using var cancellation = new CancellationTokenSource();
    var delivery = viewModel.DeliverAsync(appeal, cancellation.Token);
    cancellation.Cancel();
    service.PendingDelivery.SetCanceled(cancellation.Token);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery);

    Assert.False(viewModel.IsDeliveryAmbiguous(appeal));
    Assert.DoesNotContain("detail:appeal-1", service.Calls);
  }

  [Fact]
  public async Task LoadingGuardsDuplicateRequestsContainsStaleCancellationAndPreservesRowsOnPaginationFailure()
  {
    var service = new ModerationAppealsTestService();
    var pending = new TaskCompletionSource<ModerationAppealListResponse>();
    service.PendingFetch = pending;
    var viewModel = StaffViewModel(service);
    var firstLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Single(service.Calls);
    pending.SetResult(new([service.Current], new PageInfo(null, false, null)));
    await firstLoad;

    pending = new();
    service.PendingFetch = pending;
    var staleLoad = viewModel.SelectStatusAsync(ModerationAppealStatus.Dismissed, TestContext.Current.CancellationToken);
    service.Current = service.Appeal("appeal-resolved", ModerationAppealStatus.Resolved);
    var currentLoad = viewModel.SelectStatusAsync(ModerationAppealStatus.Resolved, TestContext.Current.CancellationToken);
    pending.SetCanceled(TestContext.Current.CancellationToken);
    await Task.WhenAll(staleLoad, currentLoad);
    Assert.Equal("appeal-resolved", viewModel.Appeals.Single().Id);

    var paginationService = new ModerationAppealsTestService();
    paginationService.Pages.Enqueue(new([paginationService.Current], new PageInfo("next", true, null)));
    var paginationViewModel = StaffViewModel(paginationService);
    await paginationViewModel.LoadAsync(TestContext.Current.CancellationToken);
    paginationService.NextError = new HttpRequestException("load more failed");
    await paginationViewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Single(paginationViewModel.Appeals);
    Assert.Equal(LoadState.Loaded, paginationViewModel.State);
    Assert.Equal("load more failed", paginationViewModel.ErrorMessage);
  }

  [Fact]
  public async Task ResolutionEnforcesModeratorSuspensionRuleAndReplacesServerState()
  {
    var service = new ModerationAppealsTestService
    {
      Current = new ModerationAppealsTestService().Appeal(suspension: true),
    };
    var viewModel = new ModerationAppealsViewModel(service, new(true, ["moderator"]));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    Assert.False(viewModel.CanResolve(appeal, ModerationAppealAction.Reduce));
    service.Current = service.Current with { SentAt = DateTimeOffset.UtcNow };
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    appeal = viewModel.Appeals.Single();
    Assert.False(viewModel.CanResolve(appeal, ModerationAppealAction.Accept));
    Assert.True(viewModel.CanResolve(appeal, ModerationAppealAction.Reduce));
    await viewModel.ResolveAsync(appeal, ModerationAppealAction.Reduce, TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.Appeals);
    Assert.Contains("resolve:appeal-1:Reduce", service.Calls);
  }

  private static ModerationAppealsViewModel StaffViewModel(ModerationAppealsTestService service) =>
      new(service, new NavigationViewer(true, ["administrator"]));
}

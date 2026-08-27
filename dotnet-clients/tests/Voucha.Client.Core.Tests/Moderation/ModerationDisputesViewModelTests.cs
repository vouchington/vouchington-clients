using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationDisputesViewModelTests
{
  [Fact]
  public async Task StaffFiltersKeepIndependentOpaqueCursorLoads()
  {
    var service = new ModerationDisputesTestService();
    var first = service.Current;
    var second = first with { Id = "dispute-2" };
    service.Pages.Enqueue(new([first], new PageInfo("opaque-pending", true, null)));
    service.Pages.Enqueue(new([first, second], new PageInfo(null, false, null)));
    service.Pages.Enqueue(new([first], new PageInfo("opaque-resolved", true, null)));
    service.Pages.Enqueue(new([first, second], new PageInfo(null, false, null)));
    service.Pages.Enqueue(new([first], new PageInfo("opaque-dismissed", true, null)));
    service.Pages.Enqueue(new([first, second], new PageInfo(null, false, null)));
    var viewModel = Staff(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    var pendingIds = viewModel.Disputes.Select(item => item.Id).ToArray();
    await viewModel.SelectStatusAsync(
        ModerationDisputeStatus.Resolved,
        TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectStatusAsync(
        ModerationDisputeStatus.Dismissed,
        TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([first.Id, second.Id], pendingIds);
    Assert.Contains("list:Pending:opaque-pending", service.Calls);
    Assert.Contains("list:Resolved:-", service.Calls);
    Assert.Contains("list:Resolved:opaque-resolved", service.Calls);
    Assert.Contains("list:Dismissed:-", service.Calls);
    Assert.Contains("list:Dismissed:opaque-dismissed", service.Calls);
    Assert.Equal(ModerationDisputeStatus.Dismissed, viewModel.SelectedStatus);
  }

  [Fact]
  public async Task ResponseAndAnnotationDraftsStaySeparateAndApprovalSavesFirst()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();
    viewModel.SetPublicResponseDraft(dispute, "Moderator response");
    viewModel.SetAnnotationDraft(dispute, "Public annotation");

    await viewModel.ApproveAsync(dispute, TestContext.Current.CancellationToken);

    Assert.Equal("Moderator response", service.Current.PublicResponse);
    Assert.Equal("Public annotation", viewModel.AnnotationDraftFor(service.Current));
    Assert.True(service.Calls.IndexOf($"update:{dispute.Id}:Moderator response") <
        service.Calls.IndexOf($"approve:{dispute.Id}"));
  }

  [Fact]
  public async Task ResolutionRequiresDeliveryAndAnnotationBody()
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
    var dispute = viewModel.Disputes.Single();

    Assert.False(viewModel.CanResolve(dispute, ModerationDisputeResolutionAction.Annotate));
    viewModel.SetAnnotationDraft(dispute, "  Context for readers  ");
    Assert.True(viewModel.CanResolve(dispute, ModerationDisputeResolutionAction.Annotate));

    await viewModel.ResolveAsync(
        dispute,
        ModerationDisputeResolutionAction.Annotate,
        TestContext.Current.CancellationToken);

    Assert.Contains(
        $"resolve:{dispute.Id}:Annotate:Context for readers",
        service.Calls);
    Assert.Empty(viewModel.Disputes);
  }

  [Fact]
  public async Task AnnotationResolutionEnforcesUtf16UnitLimit()
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
    var dispute = viewModel.Disputes.Single();
    var maximum = string.Concat(Enumerable.Repeat("😀", 1000));
    Assert.Equal(ModerationDisputesViewModel.AnnotationBodyMaximumLength, maximum.Length);

    viewModel.SetAnnotationDraft(dispute, maximum + "a");
    Assert.False(viewModel.CanResolve(dispute, ModerationDisputeResolutionAction.Annotate));
    await viewModel.ResolveAsync(
        dispute,
        ModerationDisputeResolutionAction.Annotate,
        TestContext.Current.CancellationToken);
    Assert.DoesNotContain(service.Calls, call =>
        call.StartsWith($"resolve:{dispute.Id}:", StringComparison.Ordinal));

    viewModel.SetAnnotationDraft(dispute, maximum);
    Assert.True(viewModel.CanResolve(dispute, ModerationDisputeResolutionAction.Annotate));
    await viewModel.ResolveAsync(
        dispute,
        ModerationDisputeResolutionAction.Annotate,
        TestContext.Current.CancellationToken);
    Assert.Contains($"resolve:{dispute.Id}:Annotate:{maximum}", service.Calls);
  }

  [Fact]
  public async Task AmbiguousApprovalBlocksActionsUntilTargetedRefresh()
  {
    var service = new ModerationDisputesTestService
    {
      Current = ModerationDisputesTestService.Fixture() with { PublicResponse = "Ready" },
    };
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();
    service.FailNext(ModerationDisputesTestService.AmbiguousFailure());

    await viewModel.ApproveAsync(dispute, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    Assert.False(viewModel.CanApprove(dispute));
    Assert.Equal(
        UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftModerationReportsActionFailed),
        viewModel.ErrorMessage);
    Assert.DoesNotContain("gateway failure", viewModel.ErrorMessage, StringComparison.Ordinal);
    service.Refreshes.Enqueue(service.Current with { ApprovedAt = DateTimeOffset.UtcNow });

    await viewModel.RefreshAmbiguousAsync(dispute.Id, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsMutationAmbiguous(service.Current));
    Assert.NotNull(viewModel.Disputes.Single().ApprovedAt);
  }

  [Fact]
  public async Task LoadingFailureUsesLocalizedPresentationText()
  {
    var service = new ModerationDisputesTestService();
    service.FailNext(new HttpRequestException("private upstream diagnostic"));
    var viewModel = Staff(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(
        UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftModerationReportsActionFailed),
        viewModel.ErrorMessage);
    Assert.DoesNotContain(
        "private upstream diagnostic",
        viewModel.ErrorMessage,
        StringComparison.Ordinal);
  }

  [Fact]
  public async Task AmbiguousDeliveryBlocksActionsUntilTargetedRefresh()
  {
    var service = new ModerationDisputesTestService
    {
      Current = ModerationDisputesTestService.Fixture() with
      {
        PublicResponse = "Ready",
        ApprovedAt = DateTimeOffset.UtcNow,
      },
    };
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();
    service.FailNext(ModerationDisputesTestService.AmbiguousFailure());

    await viewModel.DeliverAsync(dispute, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    Assert.False(viewModel.CanDeliver(dispute));
    service.Refreshes.Enqueue(service.Current with { SentAt = DateTimeOffset.UtcNow });

    await viewModel.RefreshAmbiguousAsync(dispute.Id, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsMutationAmbiguous(service.Current));
    Assert.NotNull(viewModel.Disputes.Single().SentAt);
  }

  [Theory]
  [InlineData(ModerationDisputeResolutionAction.Remove)]
  [InlineData(ModerationDisputeResolutionAction.Annotate)]
  [InlineData(ModerationDisputeResolutionAction.Dismiss)]
  public async Task AmbiguousResolutionBlocksRetriesUntilTargetedRefresh(
      ModerationDisputeResolutionAction action)
  {
    var service = new ModerationDisputesTestService
    {
      Current = ModerationDisputesTestService.Fixture() with
      {
        ApprovedAt = DateTimeOffset.UtcNow,
        SentAt = DateTimeOffset.UtcNow,
      },
      NextResolveResponseError = ModerationDisputesTestService.AmbiguousFailure(),
    };
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();
    if (action == ModerationDisputeResolutionAction.Annotate)
      viewModel.SetAnnotationDraft(dispute, "Member-visible context");

    await viewModel.ResolveAsync(dispute, action, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    Assert.False(viewModel.CanResolve(dispute, action));
    Assert.Single(viewModel.Disputes);
    service.Refreshes.Enqueue(service.Current);

    await viewModel.RefreshAmbiguousAsync(dispute.Id, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsMutationAmbiguous(dispute));
    Assert.Empty(viewModel.Disputes);
    Assert.Single(service.Calls, call =>
        call == $"resolve:{dispute.Id}:{action}:" +
        (action == ModerationDisputeResolutionAction.Annotate
            ? "Member-visible context"
            : "-"));
    Assert.Single(service.Calls, call => call == $"fetch:{dispute.Id}");
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task CallerCancellationMarksApprovalAndDeliveryAmbiguous(bool deliver)
  {
    var service = new ModerationDisputesTestService
    {
      Current = ModerationDisputesTestService.Fixture() with
      {
        PublicResponse = "Ready",
        ApprovedAt = deliver ? DateTimeOffset.UtcNow : null,
      },
    };
    var viewModel = Staff(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    service.FailNext(new OperationCanceledException(cancellation.Token));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        deliver
            ? viewModel.DeliverAsync(dispute, cancellation.Token)
            : viewModel.ApproveAsync(dispute, cancellation.Token));

    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    service.Refreshes.Enqueue(service.Current with
    {
      ApprovedAt = DateTimeOffset.UtcNow,
      SentAt = deliver ? DateTimeOffset.UtcNow : null,
    });
    await viewModel.RefreshAmbiguousAsync(dispute.Id, TestContext.Current.CancellationToken);
    Assert.False(viewModel.IsMutationAmbiguous(viewModel.Disputes.Single()));
  }

  [Fact]
  public async Task RerunPollingIsAttemptBoundedAndCancellationBounded()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = Staff(service, attempts: 2, delay: _ => Task.CompletedTask);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = viewModel.Disputes.Single();

    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);

    Assert.Equal(2, service.Calls.Count(call => call == $"fetch:{dispute.Id}"));
    Assert.IsType<TimeoutException>(viewModel.LastMutationError);
    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    Assert.False(viewModel.CanRerun(dispute));
    Assert.Equal(
        UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing),
        viewModel.ErrorMessage);
    service.Refreshes.Enqueue(dispute);
    await viewModel.RefreshAmbiguousAsync(
        dispute.Id,
        TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    var advanced = dispute with
    {
      AiDraftedAt = (dispute.AiDraftedAt ?? DateTimeOffset.UtcNow).AddMinutes(1),
      LatestLifecycleChangeId = "rerun-lifecycle-change",
    };
    service.Refreshes.Enqueue(advanced);
    await viewModel.RefreshAmbiguousAsync(
        dispute.Id,
        TestContext.Current.CancellationToken);
    Assert.False(viewModel.IsMutationAmbiguous(advanced));
    Assert.True(viewModel.CanRerun(advanced));

    var rejectedService = new ModerationDisputesTestService { QueueAccepted = false };
    var rejected = Staff(rejectedService);
    await rejected.LoadAsync(TestContext.Current.CancellationToken);
    await rejected.RerunAsync(
        rejected.Disputes.Single(),
        TestContext.Current.CancellationToken);
    Assert.Equal(
        UiLocalization.English.Localize(
            UiMessageKey.NativeSwiftReviewDisputesRerunNotQueued),
        rejected.ErrorMessage);

    using var cancellation = new CancellationTokenSource();
    var cancelling = Staff(
        new ModerationDisputesTestService(),
        attempts: 20,
        delay: token => Task.Delay(Timeout.InfiniteTimeSpan, token));
    await cancelling.LoadAsync(TestContext.Current.CancellationToken);
    var rerun = cancelling.RerunAsync(cancelling.Disputes.Single(), cancellation.Token);
    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rerun);
    Assert.False(cancelling.IsMutating);
  }

  [Fact]
  public async Task RerunReconcilesWhenAnotherModeratorApprovesOrResolves()
  {
    var approvalService = new ModerationDisputesTestService();
    var approval = Staff(approvalService, attempts: 1);
    await approval.LoadAsync(TestContext.Current.CancellationToken);
    var dispute = approval.Disputes.Single();
    approvalService.Refreshes.Enqueue(dispute with
    {
      ApprovedAt = DateTimeOffset.UtcNow,
      LatestLifecycleChangeId = "approval-change",
    });

    await approval.RerunAsync(dispute, TestContext.Current.CancellationToken);

    var approved = approval.Disputes.Single();
    Assert.False(approval.IsMutationAmbiguous(approved));
    Assert.True(approval.CanDeliver(approved));
    Assert.Null(approval.LastMutationError);

    var resolutionService = new ModerationDisputesTestService();
    var resolution = Staff(resolutionService, attempts: 1);
    await resolution.LoadAsync(TestContext.Current.CancellationToken);
    dispute = resolution.Disputes.Single();
    resolutionService.FailNext(ModerationDisputesTestService.AmbiguousFailure());
    await resolution.RerunAsync(dispute, TestContext.Current.CancellationToken);
    resolutionService.Refreshes.Enqueue(dispute with
    {
      Status = ModerationDisputeStatus.Resolved,
      ResolvedAt = DateTimeOffset.UtcNow,
      LatestLifecycleChangeId = "resolution-change",
    });

    await resolution.RefreshAmbiguousAsync(
        dispute.Id,
        TestContext.Current.CancellationToken);

    Assert.False(resolution.IsMutationAmbiguous(dispute));
    Assert.Empty(resolution.Disputes);
  }

  [Fact]
  public async Task MembersCannotLoadStaffDisputes()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = new ModerationDisputesViewModel(
        service,
        new NavigationViewer(true, ["member"]));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanAccess);
    Assert.Equal(LoadState.Idle, viewModel.State);
    Assert.Empty(service.Calls);
  }

  private static ModerationDisputesViewModel Staff(
      ModerationDisputesTestService service,
      int attempts = 20,
      Func<CancellationToken, Task>? delay = null) =>
      new(
          service,
          new NavigationViewer(true, ["moderator"]),
          attempts,
          delay ?? (_ => Task.CompletedTask));
}

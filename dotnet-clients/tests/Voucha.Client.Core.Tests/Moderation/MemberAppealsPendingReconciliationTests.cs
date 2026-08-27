using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class MemberAppealsPendingReconciliationTests
{
  [Fact]
  public async Task NoticePaginationReconcilesEveryPendingAppealBeforeEnablingOlderTargets()
  {
    var service = new MemberAppealsTestService();
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [], MemberAppealsFixtures.Page("pending-next", true)));
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal("appeal-2", warningId: "warning-2")],
        MemberAppealsFixtures.Page()));
    service.WarningPages.Enqueue(new(
        [Warning("warning-1")], MemberAppealsFixtures.Page("warning-next", true)));
    service.WarningPages.Enqueue(new(
        [Warning("warning-2")], MemberAppealsFixtures.Page()));
    var viewModel = new MemberAppealsViewModel(
        service,
        new NavigationViewer(true, [], IdentityId: "user-1"),
        MemberAppealsRoute.Warnings,
        new MemberAppealDraftStore());
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["warning:warning-1"],
        viewModel.EligibleTargets.Select(item => item.Id));
    Assert.Contains("appeals:Pending:pending-next", service.Calls);

    await viewModel.LoadMoreWarningsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["warning:warning-1", "warning:warning-2"],
        viewModel.WarningTargets.Select(item => item.Id));
    Assert.Equal(["warning:warning-1"],
        viewModel.EligibleTargets.Select(item => item.Id));
    Assert.Contains("appeals:Pending:", service.Calls);
    Assert.Contains("warnings:", service.Calls);
    Assert.Equal(
        ["appeals:Pending:pending-next", "warnings:warning-next"],
        service.Calls.TakeLast(2));
  }

  [Fact]
  public async Task InitialPendingFailurePreservesNoticeButDisablesAppeal()
  {
    var service = new MemberAppealsTestService();
    service.AppealErrors[ModerationAppealStatus.Pending] =
        new HttpRequestException("offline");
    service.WarningPages.Enqueue(new(
        [Warning("warning-1")], MemberAppealsFixtures.Page()));
    var viewModel = new MemberAppealsViewModel(
        service,
        new NavigationViewer(true, [], IdentityId: "user-1"),
        MemberAppealsRoute.Warnings,
        new MemberAppealDraftStore());

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["warning:warning-1"],
        viewModel.WarningTargets.Select(item => item.Id));
    Assert.Empty(viewModel.EligibleTargets);
    Assert.True(viewModel.HasLoadError);
  }

  [Fact]
  public async Task NoticePaginationFailsClosedWhilePendingAppealPageIsInFlight()
  {
    var service = new MemberAppealsTestService();
    service.WarningPages.Enqueue(new(
        [Warning("warning-1")], MemberAppealsFixtures.Page("warning-next", true)));
    service.WarningPages.Enqueue(new(
        [Warning("warning-2")], MemberAppealsFixtures.Page()));
    var viewModel = new MemberAppealsViewModel(
        service,
        new NavigationViewer(true, [], IdentityId: "user-1"),
        MemberAppealsRoute.Warnings,
        new MemberAppealDraftStore());
    var pendingPage = new TaskCompletionSource<ModerationAppealListResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingAppealFetch = pendingPage;
    var initialLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreWarningsAsync(TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    Assert.Single(service.Calls, call => call.StartsWith("warnings:", StringComparison.Ordinal));

    pendingPage.SetResult(new([], MemberAppealsFixtures.Page()));
    await initialLoad;
    await viewModel.LoadMoreWarningsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, service.Calls.Count(call =>
        call.StartsWith("warnings:", StringComparison.Ordinal)));
  }

  private static MemberWarningNotice Warning(string id) =>
      new(id, $"case-{id}", "user-1", null, null, "Message", null,
          DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
}

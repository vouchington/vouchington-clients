using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class MemberAppealsViewModelTests
{
  [Fact]
  public async Task TrackingLoadsIndependentAppealAndNoticeCollections()
  {
    var service = ServiceWithTargets();
    foreach (var status in Enum.GetValues<ModerationAppealStatus>())
    {
      service.AppealPages[status].Enqueue(new(
          [MemberAppealsFixtures.Appeal(status.ToString(), status)],
          MemberAppealsFixtures.Page($"{status}-next", true)));
    }
    var viewModel = ViewModel(service, MemberAppealsRoute.Tracking);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(3, viewModel.Appeals.Count);
    Assert.Equal(5, viewModel.EligibleTargets.Count);
    Assert.False(viewModel.HasMoreAppeals(ModerationAppealStatus.Pending));
    Assert.True(viewModel.HasMoreAppeals(ModerationAppealStatus.Resolved));
    Assert.True(viewModel.HasMoreAppeals(ModerationAppealStatus.Dismissed));
    Assert.True(viewModel.HasMoreWarnings);
    Assert.True(viewModel.HasMoreBans);
    Assert.True(viewModel.HasMoreRemovedPosts);
    Assert.Contains("appeals:Pending:Pending-next", service.Calls);
    Assert.Equal(8, service.Calls.Count);
  }

  [Fact]
  public async Task NoticeAndAppealCursorsContinueIndependentlyAndDeduplicate()
  {
    var service = ServiceWithTargets();
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal("appeal-1")], MemberAppealsFixtures.Page("appeal-next", true)));
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal("appeal-1"), MemberAppealsFixtures.Appeal("appeal-2")],
        MemberAppealsFixtures.Page()));
    service.WarningPages.Enqueue(new(
        [Warning("warning-1"), Warning("warning-2")], MemberAppealsFixtures.Page()));
    var viewModel = ViewModel(service, MemberAppealsRoute.Tracking);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAppealsAsync(
        ModerationAppealStatus.Pending, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreWarningsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, viewModel.Appeals.Count(item => item.Status == ModerationAppealStatus.Pending));
    Assert.Equal(2, viewModel.WarningTargets.Count);
    Assert.Contains("appeals:Pending:appeal-next", service.Calls);
    Assert.Contains("warnings:warning-next", service.Calls);
  }

  [Fact]
  public async Task ReloadReplacesStalePagesAndKeepsIndependentFailuresRetryable()
  {
    var service = ServiceWithTargets();
    service.AppealErrors[ModerationAppealStatus.Pending] =
        new HttpRequestException("pending unavailable");
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.WarningTargets);
    Assert.True(viewModel.HasLoadError);
    Assert.True(viewModel.HasAppealError(ModerationAppealStatus.Pending));

    service.WarningPages.Enqueue(new(
        [Warning("warning-2")], MemberAppealsFixtures.Page()));
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasLoadError);
    Assert.Equal(["warning:warning-2"], viewModel.WarningTargets.Select(item => item.Id));
  }

  [Fact]
  public async Task PendingFailureHidesTargetsUntilRetryEstablishesEligibility()
  {
    var service = ServiceWithTargets();
    service.AppealErrors[ModerationAppealStatus.Pending] =
        new HttpRequestException("pending unavailable");
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.EligibleTargets);
    Assert.True(viewModel.HasLoadError);
    await viewModel.LoadMoreAppealsAsync(
        ModerationAppealStatus.Pending, TestContext.Current.CancellationToken);

    Assert.Single(viewModel.EligibleTargets);
    Assert.False(viewModel.HasLoadError);
  }

  [Fact]
  public async Task FailedPendingRefreshPreservesKnownGoodEligibilitySnapshot()
  {
    var service = ServiceWithTargets();
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal("pending", warningId: "warning-1")],
        MemberAppealsFixtures.Page()));
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    Assert.Empty(viewModel.EligibleTargets);

    service.AppealErrors[ModerationAppealStatus.Pending] =
        new HttpRequestException("pending unavailable");
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.EligibleTargets);
    Assert.Contains(viewModel.Appeals, appeal => appeal.Id == "pending");
    Assert.True(viewModel.HasLoadError);
  }

  [Theory]
  [InlineData(MemberAppealsRoute.Warnings, "warnings:")]
  [InlineData(MemberAppealsRoute.Bans, "bans:")]
  [InlineData(MemberAppealsRoute.RemovedPosts, "removals:")]
  [InlineData(MemberAppealsRoute.Suspension, "identity")]
  public async Task NoticeRoutesLoadOnlyPendingAndTheirRelevantNotice(
      MemberAppealsRoute route,
      string relevantCall)
  {
    var service = ServiceWithTargets();
    service.AppealErrors[ModerationAppealStatus.Resolved] =
        new HttpRequestException("irrelevant");
    var viewModel = ViewModel(service, route);

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.Calls.Count);
    Assert.Contains("appeals:Pending:", service.Calls);
    Assert.Contains(relevantCall, service.Calls);
    Assert.DoesNotContain(service.Calls, call =>
        call.StartsWith("appeals:Resolved:", StringComparison.Ordinal) ||
        call.StartsWith("appeals:Dismissed:", StringComparison.Ordinal));
    Assert.False(viewModel.HasLoadError);
  }

  [Fact]
  public async Task PaginationFailureRetainsCursorAndSucceedsOnRetry()
  {
    var service = ServiceWithTargets();
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    service.WarningError = new HttpRequestException("page unavailable");

    await viewModel.LoadMoreWarningsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.WarningPagination.HasError);
    Assert.True(viewModel.WarningPagination.HasMore);
    service.WarningPages.Enqueue(new(
        [Warning("warning-2")], MemberAppealsFixtures.Page()));
    await viewModel.LoadMoreWarningsAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.WarningPagination.HasError);
    Assert.Equal(2, viewModel.WarningTargets.Count);
  }

  [Fact]
  public async Task InitialFailuresRetryTheirOwnStreamsWithoutCursors()
  {
    var service = ServiceWithTargets();
    service.AppealErrors[ModerationAppealStatus.Pending] =
        new HttpRequestException("pending unavailable");
    service.WarningError = new HttpRequestException("warnings unavailable");
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(
        new MemberAppealPaginationState(false, false, true),
        viewModel.AppealPagination(ModerationAppealStatus.Pending));
    Assert.Equal(
        new MemberAppealPaginationState(false, false, true),
        viewModel.WarningPagination);
    service.WarningPages.Clear();
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal("recovered")], MemberAppealsFixtures.Page()));
    service.WarningPages.Enqueue(new(
        [Warning("warning-recovered")], MemberAppealsFixtures.Page()));

    await viewModel.LoadMoreAppealsAsync(
        ModerationAppealStatus.Pending, TestContext.Current.CancellationToken);
    await viewModel.LoadMoreWarningsAsync(TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.Appeals, appeal => appeal.Id == "recovered");
    Assert.Equal(["warning:warning-recovered"], viewModel.WarningTargets.Select(item => item.Id));
    Assert.Equal(2, service.Calls.Count(call =>
        call == "appeals:Pending:"));
    Assert.Equal(2, service.Calls.Count(call => call == "warnings:"));
  }

  [Fact]
  public async Task TargetsKeepPlatformCommunityAndSuspensionDistinct()
  {
    var viewModel = ViewModel(ServiceWithTargets(), MemberAppealsRoute.Tracking);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Contains(viewModel.EligibleTargets, target =>
        target.Id == "removal:platform:post-1");
    Assert.Contains(viewModel.EligibleTargets, target =>
        target.Id == "removal:community:post-1");
    Assert.Contains(viewModel.EligibleTargets, target =>
        target.TargetType == ModerationAppealTargetType.Suspension && target.TargetId is null);
  }

  [Fact]
  public async Task RevokedWarningsAreNotEligible()
  {
    var service = new MemberAppealsTestService();
    service.WarningPages.Enqueue(new(
        [
          Warning("active"),
          Warning("revoked") with { RevokedAt = DateTimeOffset.UtcNow },
        ],
        MemberAppealsFixtures.Page()));
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["warning:active"], viewModel.EligibleTargets.Select(item => item.Id));
  }

  [Fact]
  public void DraftsAreNamespacedByIdentityAndTarget()
  {
    var store = new MemberAppealDraftStore();
    var target = MemberAppealTarget.Warning("warning-1", null, null, DateTimeOffset.UtcNow);
    store.SetReason("user-1", target, ModerationAppealReason.WrongRule);
    store.SetDetails("user-1", target, "First account");

    Assert.Equal("First account", store.Get("user-1", target).Details);
    Assert.Equal(string.Empty, store.Get("user-2", target).Details);
  }

  [Fact]
  public void SuspensionDraftsAreScopedToTheSuspensionInstance()
  {
    var store = new MemberAppealDraftStore();
    var first = MemberAppealTarget.Suspension(
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
    var second = MemberAppealTarget.Suspension(
        DateTimeOffset.Parse("2026-08-01T00:00:00Z"));
    store.SetReason("user-1", first, ModerationAppealReason.Other);
    store.SetDetails("user-1", first, "Previous suspension");

    Assert.NotEqual(first.Id, second.Id);
    Assert.Equal(new MemberAppealDraft(), store.Get("user-1", second));
  }

  [Fact]
  public async Task SubmissionIsExactlyOnceAndDuplicateBecomesTracked()
  {
    var service = ServiceWithTargets();
    var pending = new TaskCompletionSource<ModerationAppealSubmissionResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingSubmission = pending;
    service.Submission = new(MemberAppealsFixtures.Appeal("duplicate", warningId: "warning-1"), true);
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var target = Assert.Single(viewModel.EligibleTargets);
    viewModel.BeginAppeal(target);
    viewModel.SetReason(ModerationAppealReason.IncorrectFacts);
    viewModel.SetDetails("The warning used the wrong facts.");

    var first = viewModel.SubmitAsync("turnstile-1", TestContext.Current.CancellationToken);
    var second = viewModel.SubmitAsync("turnstile-2", TestContext.Current.CancellationToken);
    Assert.True(second.IsCompletedSuccessfully);
    pending.SetResult(service.Submission);
    await first;

    Assert.Single(service.Calls, call => call.StartsWith("submit:", StringComparison.Ordinal));
    Assert.True(viewModel.LastSubmissionWasDuplicate);
    Assert.Contains(viewModel.Appeals, appeal => appeal.Id == "duplicate");
    Assert.Null(viewModel.ActiveTarget);
  }

  [Fact]
  public async Task FailedSubmissionRetainsDraftForFreshTurnstileRetry()
  {
    var service = ServiceWithTargets();
    service.SubmissionError = new HttpRequestException("verification failed");
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var target = Assert.Single(viewModel.EligibleTargets);
    viewModel.BeginAppeal(target);
    viewModel.SetReason(ModerationAppealReason.Other);
    viewModel.SetDetails("Please review this warning.");

    await viewModel.SubmitAsync("expired-token", TestContext.Current.CancellationToken);
    Assert.Equal(
        MemberAppealSubmissionOutcome.Failed,
        viewModel.SubmissionOutcome);
    Assert.Equal(
        UiMessageKey.NativeSwiftModerationAppealsMemberGenericError,
        viewModel.SubmissionMessageKey);
    await viewModel.SubmitAsync("fresh-token", TestContext.Current.CancellationToken);

    Assert.Equal(2, service.Calls.Count(call => call.StartsWith("submit:", StringComparison.Ordinal)));
    Assert.Contains(service.Calls, call => call.EndsWith(":fresh-token", StringComparison.Ordinal));
    Assert.Null(viewModel.ActiveTarget);
    Assert.Equal(
        MemberAppealSubmissionOutcome.Submitted,
        viewModel.SubmissionOutcome);
  }

  [Fact]
  public async Task CanceledSubmissionRetainsDraftAndResetsState()
  {
    var service = ServiceWithTargets();
    service.PendingSubmission = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    var target = Assert.Single(viewModel.EligibleTargets);
    viewModel.BeginAppeal(target);
    viewModel.SetReason(ModerationAppealReason.ContextMissing);
    viewModel.SetDetails("Keep this draft after cancellation.");
    using var cancellation = new CancellationTokenSource();

    var submission = viewModel.SubmitAsync("token", cancellation.Token);
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => submission);
    Assert.False(viewModel.IsSubmitting);
    Assert.Equal(MemberAppealSubmissionOutcome.Idle, viewModel.SubmissionOutcome);
    Assert.Equal(target, viewModel.ActiveTarget);
    Assert.Equal(
        "Keep this draft after cancellation.",
        viewModel.ActiveDraft.Details);
  }

  [Theory]
  [InlineData(ModerationAppealReason.IncorrectFacts)]
  [InlineData(ModerationAppealReason.WrongRule)]
  [InlineData(ModerationAppealReason.ContextMissing)]
  [InlineData(ModerationAppealReason.Disproportionate)]
  [InlineData(ModerationAppealReason.Other)]
  public async Task SubmitsEveryCanonicalReason(ModerationAppealReason reason)
  {
    var service = ServiceWithTargets();
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginAppeal(Assert.Single(viewModel.EligibleTargets));
    viewModel.SetReason(reason);
    viewModel.SetDetails("Reason-specific details.");

    await viewModel.SubmitAsync("token", TestContext.Current.CancellationToken);

    Assert.Equal(reason, service.LastSubmissionRequest?.Reason);
  }

  [Fact]
  public async Task ValidationAndDuplicateOutcomesAreTyped()
  {
    var service = ServiceWithTargets();
    service.Submission = new(
        MemberAppealsFixtures.Appeal("duplicate", warningId: "warning-1"),
        true);
    var viewModel = ViewModel(service, MemberAppealsRoute.Warnings);
    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginAppeal(Assert.Single(viewModel.EligibleTargets));

    await viewModel.SubmitAsync("unused", TestContext.Current.CancellationToken);
    Assert.Equal(
        MemberAppealSubmissionOutcome.ReasonRequired,
        viewModel.SubmissionOutcome);
    viewModel.SetReason(ModerationAppealReason.Other);
    await viewModel.SubmitAsync("unused", TestContext.Current.CancellationToken);
    Assert.Equal(
        MemberAppealSubmissionOutcome.DetailsRequired,
        viewModel.SubmissionOutcome);
    viewModel.SetDetails(new string('a', 3801));
    await viewModel.SubmitAsync("unused", TestContext.Current.CancellationToken);
    Assert.Equal(
        MemberAppealSubmissionOutcome.DetailsTooLong,
        viewModel.SubmissionOutcome);
    viewModel.SetDetails("Please review this warning.");
    await viewModel.SubmitAsync("token", TestContext.Current.CancellationToken);
    Assert.Equal(
        MemberAppealSubmissionOutcome.Duplicate,
        viewModel.SubmissionOutcome);
    Assert.Equal(
        UiMessageKey.NativeSwiftModerationAppealsMemberPendingExists,
        viewModel.SubmissionMessageKey);
  }

  [Fact]
  public async Task SignedOutViewerDoesNotLoadMemberData()
  {
    var service = ServiceWithTargets();
    var viewModel = new MemberAppealsViewModel(
        service, NavigationViewer.Anonymous, MemberAppealsRoute.Tracking);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Empty(service.Calls);
  }

  private static MemberAppealsViewModel ViewModel(
      MemberAppealsTestService service,
      MemberAppealsRoute route) =>
      new(service, new NavigationViewer(true, [], IdentityId: "user-1"), route,
          new MemberAppealDraftStore());

  private static MemberAppealsTestService ServiceWithTargets()
  {
    var service = new MemberAppealsTestService
    {
      Identity = new MyIdentityResponse(new User(
          "user-1", "member",
          SuspendedAt: DateTimeOffset.Parse("2026-07-01T00:00:00Z"))),
    };
    service.WarningPages.Enqueue(new(
        [Warning("warning-1")], MemberAppealsFixtures.Page("warning-next", true)));
    service.BanPages.Enqueue(new(
        [new("ban-1", "community-1", "community", "user-1", "reason", null,
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"), null)],
        MemberAppealsFixtures.Page("ban-next", true)));
    service.RemovalPages.Enqueue(new(
        [
          new("post-1", "Post", "community-1", "community",
              DateTimeOffset.Parse("2026-07-01T00:00:00Z"), "platform"),
          new("post-1", "Post", "community-1", "community",
              DateTimeOffset.Parse("2026-07-01T00:00:00Z"), "community"),
        ],
        MemberAppealsFixtures.Page("removal-next", true)));
    return service;
  }

  private static MemberWarningNotice Warning(string id) =>
      new(id, $"case-{id}", "user-1", null, null, "Message", null,
          DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
}

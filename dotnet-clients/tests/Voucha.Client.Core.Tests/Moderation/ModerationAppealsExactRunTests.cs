using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ModerationAppealsViewModelTests
{
  [Fact]
  public async Task MismatchedTerminalRunCannotReleaseAdmissionButMatchingFailureCan()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    service.Runs.Enqueue(Run(service.RunId, ModerationRunStatus.Succeeded) with { RunId = "newer-run" });
    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));

    service.Runs.Enqueue(Run(service.RunId, ModerationRunStatus.Failed));
    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task MismatchedAdmissionPollStopsWithoutReleasingOrRepeatingPost()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 3);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.Runs.Enqueue(Run(service.RunId, ModerationRunStatus.Failed) with { RunId = "another-generation" });

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.Equal(1, service.Calls.Count(call => call.StartsWith("run:", StringComparison.Ordinal)));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Theory]
  [InlineData(ModerationRunStatus.Failed)]
  [InlineData(ModerationRunStatus.Expired)]
  public async Task TerminalFailureReleasesTheExactAdmission(ModerationRunStatus status)
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.Runs.Enqueue(Run(service.RunId, status));

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.True(viewModel.CanRerun(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Theory]
  [InlineData(ModerationRunStatus.Failed)]
  [InlineData(ModerationRunStatus.Expired)]
  public async Task RefreshReleasesOnlyTheMatchingTerminalRun(ModerationRunStatus status)
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    service.Runs.Enqueue(Run(service.RunId, status));

    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task AmbiguousPostCanReleaseOnlyAfterTargetedReadFindsTerminalMutation()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.NextError = new HttpRequestException("response lost");
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));

    service.Details.Enqueue(appeal with { ApprovedAt = DateTimeOffset.UtcNow });
    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
    Assert.Equal(2, service.Calls.Count(call => call == $"detail:{appeal.Id}"));
    Assert.DoesNotContain(service.Calls, call => call.StartsWith("run:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task CompletedRunStillReconcilesAnObservedTerminalAppealMutation()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.Runs.Enqueue(Run(service.RunId, ModerationRunStatus.Succeeded));
    service.Details.Enqueue(appeal with { ApprovedAt = DateTimeOffset.UtcNow });

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.NotNull(viewModel.Appeals.Single().ApprovedAt);
  }

  [Fact]
  public async Task CancellationAfterIgnoredRunReadLeavesAdmissionPending()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.PendingRun = new TaskCompletionSource<ModerationAppealResolutionDraftRunResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var heldRun = service.PendingRun;
    using var cancellation = new CancellationTokenSource();
    var rerun = viewModel.RerunAsync(appeal, cancellation.Token);
    cancellation.Cancel();
    heldRun!.SetResult(Run(service.RunId, ModerationRunStatus.Failed));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rerun);

    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task CancellationAfterIgnoredRefreshReadDoesNotReleaseTerminalRun()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    service.PendingRun = new TaskCompletionSource<ModerationAppealResolutionDraftRunResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var heldRun = service.PendingRun;
    using var cancellation = new CancellationTokenSource();
    var refresh = viewModel.RefreshPendingAIRerunAsync(appeal, cancellation.Token);
    cancellation.Cancel();
    heldRun!.SetResult(Run(service.RunId, ModerationRunStatus.Expired));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);

    Assert.True(viewModel.IsAIRerunPending(appeal));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }
}

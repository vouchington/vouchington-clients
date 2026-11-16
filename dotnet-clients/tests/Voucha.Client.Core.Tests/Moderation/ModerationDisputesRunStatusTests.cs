using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationDisputesRunStatusTests
{
  private const string QueuedRun = "dispute-rerun-job-1";

  [Theory]
  [InlineData(ModerationRunStatus.Active, "native.swift.reviewDisputes.rerunStillProcessing")]
  [InlineData(ModerationRunStatus.Succeeded, null)]
  [InlineData(ModerationRunStatus.Failed, "native.swift.reviewDisputes.rerunFailed")]
  [InlineData(ModerationRunStatus.Expired, "native.swift.reviewDisputes.rerunExpired")]
  public async Task WithoutAnAdmissionTheRowStatusMapsToCopy(ModerationRunStatus status, string? key)
  {
    var viewModel = await LoadedAsync(new ModerationDisputesTestService());
    var dispute = viewModel.Disputes.Single() with { AiDraftRequestedRunStatus = status };

    Assert.Equal(key, viewModel.AIDraftStatusKey(dispute)?.Value);
  }

  [Fact]
  public async Task NullStatusShowsNothing()
  {
    var viewModel = await LoadedAsync(new ModerationDisputesTestService());

    Assert.Null(viewModel.AIDraftStatusKey(viewModel.Disputes.Single() with { AiDraftRequestedRunStatus = null }));
  }

  [Fact]
  public void ViewersWithoutAccessGetNoStatusCopy()
  {
    var viewModel = new ModerationDisputesViewModel(
        new ModerationDisputesTestService(), new NavigationViewer(true, ["member"]));
    var dispute = ModerationDisputesTestService.Fixture() with
    {
      AiDraftRequestedRunStatus = ModerationRunStatus.Failed,
    };

    Assert.Null(viewModel.AIDraftStatusKey(dispute));
  }

  [Fact]
  public async Task StaleRowStatusWhileAnotherRunIsPendingShowsActive()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = await LoadedAsync(service);
    var dispute = viewModel.Disputes.Single();
    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);

    var stale = dispute with { AiDraftRequestedRunId = "older-run", AiDraftRequestedRunStatus = ModerationRunStatus.Failed };

    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    Assert.Equal("native.swift.reviewDisputes.rerunStillProcessing", viewModel.AIDraftStatusKey(stale)?.Value);
    Assert.Single(service.Calls, call => call == $"rerun:{dispute.Id}");
  }

  [Theory]
  [InlineData(ModerationRunStatus.Active, "native.swift.reviewDisputes.rerunStillProcessing")]
  [InlineData(ModerationRunStatus.Failed, "native.swift.reviewDisputes.rerunFailed")]
  [InlineData(ModerationRunStatus.Expired, "native.swift.reviewDisputes.rerunExpired")]
  [InlineData(ModerationRunStatus.Succeeded, null)]
  public async Task MatchingRunShowsItsOwnStatusWhilePending(ModerationRunStatus status, string? key)
  {
    var viewModel = await LoadedAsync(new ModerationDisputesTestService());
    var dispute = viewModel.Disputes.Single();
    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);

    var exact = dispute with { AiDraftRequestedRunId = QueuedRun, AiDraftRequestedRunStatus = status };

    Assert.Equal(key, viewModel.AIDraftStatusKey(exact)?.Value);
  }

  [Theory]
  [InlineData(ModerationRunStatus.Failed, "native.swift.reviewDisputes.rerunFailed")]
  [InlineData(ModerationRunStatus.Expired, "native.swift.reviewDisputes.rerunExpired")]
  public async Task PolledFailedOrExpiredExactRunEndsTheAdmissionAndKeepsItsStatus(
      ModerationRunStatus status, string key)
  {
    var service = new ModerationDisputesTestService();
    var viewModel = await LoadedAsync(service);
    var dispute = viewModel.Disputes.Single();
    service.Refreshes.Enqueue(dispute with { AiDraftRequestedRunId = QueuedRun, AiDraftRequestedRunStatus = status });

    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);

    var row = viewModel.Disputes.Single();
    Assert.False(viewModel.IsMutationAmbiguous(row));
    Assert.True(viewModel.CanRerun(row));
    Assert.Equal(status, row.AiDraftRequestedRunStatus);
    Assert.Equal(key, viewModel.AIDraftStatusKey(row)?.Value);
    Assert.Equal(UiLocalization.English.Localize(new UiMessageKey(key)), viewModel.ErrorMessage);
    Assert.Single(service.Calls, call => call == $"rerun:{dispute.Id}");
  }

  [Fact]
  public async Task AFailedRunThatIsNotTheQueuedRunDoesNotEndTheAdmission()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = await LoadedAsync(service, attempts: 1);
    var dispute = viewModel.Disputes.Single();
    service.Refreshes.Enqueue(dispute with { AiDraftRequestedRunId = "older-run", AiDraftRequestedRunStatus = ModerationRunStatus.Failed });

    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);

    Assert.IsType<TimeoutException>(viewModel.LastMutationError);
    Assert.True(viewModel.IsMutationAmbiguous(dispute));
  }

  [Fact]
  public async Task ManualRefreshOfAFailedExactRunEndsTheAdmission()
  {
    var service = new ModerationDisputesTestService();
    var viewModel = await LoadedAsync(service, attempts: 1);
    var dispute = viewModel.Disputes.Single();
    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsMutationAmbiguous(dispute));
    service.Refreshes.Enqueue(dispute with { AiDraftRequestedRunId = QueuedRun, AiDraftRequestedRunStatus = ModerationRunStatus.Failed });

    await viewModel.RefreshAmbiguousAsync(dispute.Id, TestContext.Current.CancellationToken);

    var row = viewModel.Disputes.Single();
    Assert.False(viewModel.IsMutationAmbiguous(row));
    Assert.Equal(ModerationRunStatus.Failed, row.AiDraftRequestedRunStatus);
    Assert.Equal("native.swift.reviewDisputes.rerunFailed", viewModel.AIDraftStatusKey(row)?.Value);
  }

  [Fact]
  public async Task RegisteringAndQueueingAPendingRunNotifiesObservers()
  {
    var viewModel = await LoadedAsync(new ModerationDisputesTestService(), attempts: 1);
    var dispute = viewModel.Disputes.Single() with { AiDraftRequestedRunStatus = ModerationRunStatus.Failed };
    var changes = 0;
    viewModel.PropertyChanged += (_, args) => changes += args.PropertyName == nameof(viewModel.Disputes) ? 1 : 0;

    await viewModel.RerunAsync(dispute, TestContext.Current.CancellationToken);

    Assert.True(changes >= 2);
  }

  private static async Task<ModerationDisputesViewModel> LoadedAsync(
      ModerationDisputesTestService service, int attempts = 20)
  {
    var viewModel = new ModerationDisputesViewModel(
        service, new NavigationViewer(true, ["moderator"]), attempts, _ => Task.CompletedTask);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }
}

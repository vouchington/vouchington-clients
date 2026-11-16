using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ModerationAppealsViewModelTests
{
  [Theory]
  [InlineData(ModerationRunStatus.Active, "NativeSwiftAiRunStatusAppealDraftActive")]
  [InlineData(ModerationRunStatus.Succeeded, "NativeSwiftAiRunStatusAppealDraftSucceeded")]
  [InlineData(ModerationRunStatus.Failed, "NativeSwiftAiRunStatusAppealDraftFailed")]
  [InlineData(ModerationRunStatus.Expired, "NativeSwiftAiRunStatusAppealDraftExpired")]
  public async Task ServerReportedRunStatusMapsToCopyWithoutAnAdmission(ModerationRunStatus status, string name)
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single() with { AiDraftRequestedRunStatus = status };

    Assert.Equal(name, KeyName(viewModel.AIDraftStatusKey(appeal)));
    Assert.Null(viewModel.AIDraftStatusKey(appeal with { AiDraftRequestedRunStatus = null }));
  }

  [Fact]
  public async Task PendingRunStaysActiveUntilTheAppealNamesTheAdmittedRun()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsAIRerunPending(appeal));

    var stale = appeal with { AiDraftRequestedRunId = "newer-run", AiDraftRequestedRunStatus = ModerationRunStatus.Failed };
    Assert.Equal("NativeSwiftAiRunStatusAppealDraftActive", KeyName(viewModel.AIDraftStatusKey(stale)));
    var exact = appeal with { AiDraftRequestedRunId = service.RunId, AiDraftRequestedRunStatus = ModerationRunStatus.Failed };
    Assert.Equal("NativeSwiftAiRunStatusAppealDraftFailed", KeyName(viewModel.AIDraftStatusKey(exact)));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Theory]
  [InlineData(ModerationRunStatus.Failed, "NativeSwiftAiRunStatusAppealDraftFailed")]
  [InlineData(ModerationRunStatus.Expired, "NativeSwiftAiRunStatusAppealDraftExpired")]
  public async Task PolledTerminalRunIsKeptOnTheRowAfterRefreshReleasesTheAdmission(
      ModerationRunStatus status, string name)
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    service.Runs.Enqueue(Run(service.RunId, status));

    await viewModel.RefreshPendingAIRerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.Equal(name, KeyName(viewModel.AIDraftStatusKey(viewModel.Appeals.Single())));
    Assert.Single(service.Calls, call => call == $"rerun:{appeal.Id}");
  }

  [Fact]
  public async Task PolledFailureDuringRerunIsKeptOnTheRow()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 1);
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    service.Runs.Enqueue(Run(service.RunId, ModerationRunStatus.Expired));

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.False(viewModel.IsAIRerunPending(appeal));
    Assert.Equal("NativeSwiftAiRunStatusAppealDraftExpired", KeyName(viewModel.AIDraftStatusKey(viewModel.Appeals.Single())));
  }

  [Fact]
  public async Task ViewersWithoutAccessGetNoRunStatusCopy()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = new Voucha.Client.Core.Moderation.ModerationAppealsViewModel(
        service, new Voucha.Client.Core.Navigation.NavigationViewer(false, []), 1, _ => Task.CompletedTask);
    var appeal = service.Appeal(response: "Ready") with { AiDraftRequestedRunStatus = ModerationRunStatus.Failed };

    Assert.Null(viewModel.AIDraftStatusKey(appeal));
    await Task.CompletedTask;
  }

  private static string? KeyName(UiMessageKey? key) =>
      key is null ? null : typeof(UiMessageKey).GetFields()
          .Single(field => field.FieldType == typeof(UiMessageKey) && Equals(field.GetValue(null), key)).Name;
}

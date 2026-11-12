using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Tests.Moderation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ReportJudgementPendingPageTests
{
  [Fact]
  public async Task AdmittedRunRendersPendingRefreshAndDisablesDuplicateAdmission()
  {
    var service = new Service();
    var model = new ModerationReportsViewModel(service, new NavigationViewer(true, ["administrator"]),
        judgementRunReads: 1, judgementRunDelay: _ => Task.CompletedTask);
    await model.SetFiltersAsync(ModerationReportStatus.Pending, ModerationReportsMode.Flat,
        ModerationReportSort.CreatedAtDesc, TestContext.Current.CancellationToken);
    UiCopy.UseLocalization(UiLocalization.English);
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    foreach (var key in UiMessageKey.All)
      Application.Current!.Resources[key.Value] = UiLocalization.English.Localize(key);
    var page = new ModerationReportsPage(model);
    var rerun = Buttons(page, "Re-run judgement").First();
    rerun.SendClicked();
    await PageTestWait.WaitUntilAsync(() => service.Reads == 1 && !model.IsQueueMutationRunning);
    Assert.False(Buttons(page, "Re-run judgement").First().IsEnabled);
    Assert.Contains(VisualTreeElements.Descendants<Label>(page), label => label.Text == "Pending");
    var refresh = Buttons(page, "Refresh").Single();
    Assert.True(refresh.IsEnabled);
    refresh.SendClicked();
    await PageTestWait.WaitUntilAsync(() => service.Reads == 2 && !model.IsQueueMutationRunning);
    Assert.Equal(1, service.Posts);
    Assert.True(model.IsJudgementPending(service.ReportId));
  }

  private static IEnumerable<Button> Buttons(Page page, string text) =>
      VisualTreeElements.Descendants<Button>(page).Where(button => button.Text == text);

  private sealed class Service : ReviewQueueModerationServiceStub
  {
    private readonly StaffFlatModerationReportsResponse reports = JsonSerializer.Deserialize<StaffFlatModerationReportsResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.reports.default"), VouchaApiJson.Options)!;
    public string ReportId => reports.Reports[0].Id;
    public int Posts { get; private set; }
    public int Reads { get; private set; }
    public override Task<StaffFlatModerationReportsResponse> FetchStaffReportsAsync(string? status = "pending", string? sort = null,
        string? after = null, int limit = 25, CancellationToken cancellationToken = default) => Task.FromResult(reports);
    public override Task<ModerationReportJudgementResponse> RerunReportJudgementAsync(string reportId, CancellationToken cancellationToken = default)
    {
      Posts++;
      return Task.FromResult(JsonSerializer.Deserialize<ModerationReportJudgementResponse>(
          ApiFixtureLoader.LoadResponse("native.moderation.report-judgement.default"), VouchaApiJson.Options)!);
    }
    public override Task<ModerationReportJudgementRunResponse> GetReportJudgementRunAsync(string reportId, string runId,
        CancellationToken cancellationToken = default)
    {
      Reads++;
      return Task.FromResult(JsonSerializer.Deserialize<ModerationReportJudgementRunResponse>(
          ApiFixtureLoader.LoadResponse("native.moderation.report-judgement-run.pending"), VouchaApiJson.Options)!);
    }
  }
}

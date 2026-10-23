using System.Net;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationReportJudgementRunTests
{
  private const string ReportId = "019f6559-6b31-7171-b5b1-ccd9d702c45e";
  private const string AdmittedRunId = "00000000-0000-7000-8000-000000000303";
  private const string SchemaCompletedRun =
      """{"completed":true,"judgement_id":"00000000-0000-7000-8000-000000000399"}""";
  private const string SchemaIncompleteRun = """{"completed":true,"judgement_id":null}""";

  [Fact]
  public async Task PendingFixtureReadsOnlyTheAdmittedRunUntilTheBound()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture("native.moderation.report-judgement-run.pending"),
        Fixture("native.moderation.report-judgement-run.pending"),
        Fixture("native.moderation.report-judgement-run.pending"));
    var viewModel = await Loaded(handler, 3);

    Assert.True(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    Assert.Equal(3, RunGets(handler).Length);
    Assert.All(RunGets(handler), request => Assert.Equal(RunPath(AdmittedRunId), request.PathAndQuery));
    Assert.Empty(viewModel.RowErrors);
  }

  [Fact]
  public async Task SchemaCompletedRunStopsBeforeTheBoundWithoutAnotherAdmission()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture("native.moderation.report-judgement-run.pending"),
        new RecordedResponse(SchemaCompletedRun));
    var viewModel = await Loaded(handler, 4);

    Assert.True(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    Assert.Equal(2, RunGets(handler).Length);
    Assert.Equal(RunPath(AdmittedRunId), RunGets(handler)[1].PathAndQuery);
  }

  [Fact]
  public async Task CompletedRunWithoutJudgementIdStopsInsteadOfReadingAgain()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        new RecordedResponse(SchemaIncompleteRun),
        Fixture("native.moderation.report-judgement-run.pending"));
    var viewModel = await Loaded(handler, 4);

    Assert.True(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    var reads = RunGets(handler);
    Assert.Single(reads);
    Assert.Equal(RunPath(AdmittedRunId), reads[0].PathAndQuery);
  }

  [Fact]
  public async Task UnqueuedAdmissionDoesNotReadARun()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        new RecordedResponse(Admission(queued: false, runId: AdmittedRunId), HttpStatusCode.Accepted));
    var viewModel = await Loaded(handler, 3);

    Assert.True(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    Assert.Empty(RunGets(handler));
    Assert.Empty(viewModel.RowErrors);
  }

  [Fact]
  public async Task EmptyAdmittedRunIdDoesNotReadARun()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        new RecordedResponse(Admission(queued: true, runId: ""), HttpStatusCode.Accepted));
    var viewModel = await Loaded(handler, 3);

    Assert.True(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    Assert.Empty(RunGets(handler));
  }

  [Fact]
  public async Task RunReadFailureDoesNotAdmitAgain()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError));
    var viewModel = await Loaded(handler, 3);

    Assert.False(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    Assert.Single(RunGets(handler));
    Assert.NotEmpty(viewModel.RowErrors);
  }

  [Fact]
  public async Task CancellationDuringTheReadSpacingDoesNotReadAgain()
  {
    using var cancellation = new CancellationTokenSource();
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture("native.moderation.report-judgement-run.pending"),
        Fixture("native.moderation.report-judgement-run.pending"));
    var viewModel = ViewModel(handler, 3, token =>
    {
      cancellation.Cancel();
      return Task.Delay(Timeout.InfiniteTimeSpan, token);
    });
    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Pending,
        ModerationReportsMode.Flat,
        ModerationReportSort.CreatedAtDesc,
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        viewModel.RerunJudgementAsync(ReportId, cancellation.Token));

    Assert.Single(Posts(handler));
    Assert.Single(RunGets(handler));
    Assert.Empty(viewModel.RowErrors);
  }

  private static string RunPath(string runId) => $"/api/v1/reports/{ReportId}/judgements/{runId}";

  private static string Admission(bool queued, string runId)
  {
    var admission = JsonNode.Parse(ApiFixtureLoader.LoadResponse(
        "native.moderation.report-judgement.default"))!.AsObject();
    admission["queued"] = queued;
    admission["run_id"] = runId;
    return admission.ToJsonString();
  }

  private static async Task<ModerationReportsViewModel> Loaded(RecordingHandler handler, int reads)
  {
    var viewModel = ViewModel(handler, reads, _ => Task.CompletedTask);
    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Pending,
        ModerationReportsMode.Flat,
        ModerationReportSort.CreatedAtDesc,
        TestContext.Current.CancellationToken);
    return viewModel;
  }

  private static ModerationReportsViewModel ViewModel(
      RecordingHandler handler,
      int reads,
      Func<CancellationToken, Task> delay) =>
      new(
          new ApiModerationService(new VouchaApiClient(
              new HttpClient(handler) { BaseAddress = new Uri("https://api.test") })),
          new NavigationViewer(true, ["administrator"]),
          judgementRunReads: reads,
          judgementRunDelay: delay);

  private static RecordingHandler Handler(params RecordedResponse[] responses) => new(responses);

  private static RecordedResponse Fixture(string id, HttpStatusCode status = HttpStatusCode.OK) =>
      new(ApiFixtureLoader.LoadResponse(id), status);

  private static RecordedRequest[] Posts(RecordingHandler handler) =>
      handler.Requests.Where(request =>
          request.Method == HttpMethod.Post &&
          request.PathAndQuery == $"/api/v1/reports/{ReportId}/judgements").ToArray();

  private static RecordedRequest[] RunGets(RecordingHandler handler) =>
      handler.Requests.Where(request =>
          request.Method == HttpMethod.Get &&
          request.PathAndQuery?.Contains("/judgements/", StringComparison.Ordinal) == true).ToArray();
}

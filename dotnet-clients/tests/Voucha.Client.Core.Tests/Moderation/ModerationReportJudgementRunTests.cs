using System.Net;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationReportJudgementRunTests
{
  private const string ReportId = "019f6559-6b31-7171-b5b1-ccd9d702c45e";
  private const string AdmittedRunId = "00000000-0000-7000-8000-000000000303";
  private const string SucceededRun =
      """{"judgement_id":"00000000-0000-7000-8000-000000000399","run_id":"00000000-0000-7000-8000-000000000303","status":"succeeded"}""";

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
  public async Task SucceededRunStopsBeforeTheBoundWithoutAnotherAdmission()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture("native.moderation.report-judgement-run.pending"),
        new RecordedResponse(SucceededRun));
    var viewModel = await Loaded(handler, 4);

    Assert.True(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    Assert.Equal(2, RunGets(handler).Length);
    Assert.Equal(RunPath(AdmittedRunId), RunGets(handler)[1].PathAndQuery);
  }

  [Theory]
  [InlineData("failed")]
  [InlineData("expired")]
  public async Task FailedOrExpiredRunStopsAndSurfacesAFailure(string status)
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture($"native.moderation.report-judgement-run.{status}"),
        Fixture("native.moderation.report-judgement-run.pending"));
    var viewModel = await Loaded(handler, 4);

    Assert.False(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));

    Assert.Single(Posts(handler));
    var reads = RunGets(handler);
    Assert.Single(reads);
    Assert.Equal(RunPath(AdmittedRunId), reads[0].PathAndQuery);
    var error = Assert.Single(viewModel.RowErrors).Value;
    Assert.Null(error.VerbatimValue);
    Assert.Equal(
        status == "expired"
            ? UiMessageKey.NativeSwiftModerationReportsAiJudgementRunExpired
            : UiMessageKey.NativeSwiftModerationReportsAiJudgementRunFailed,
        error.Key);
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
    Assert.True(viewModel.IsJudgementPending(ReportId));
    Assert.False(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.Single(Posts(handler));
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

  [Fact]
  public async Task FailedFirstReadRetainsAdmissionAndManualRefreshNeverPostsAgain()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError),
        Fixture("native.moderation.report-judgement-run.pending"),
        Fixture("native.moderation.report-judgement-run.failed"));
    var viewModel = await Loaded(handler, 1);
    Assert.False(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.True(viewModel.IsJudgementPending(ReportId));
    Assert.False(await viewModel.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.True(await viewModel.RefreshPendingJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.True(viewModel.IsJudgementPending(ReportId));
    Assert.False(await viewModel.RefreshPendingJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.False(viewModel.IsJudgementPending(ReportId));
    Assert.Single(Posts(handler));
    Assert.Equal(3, RunGets(handler).Length);
  }

  [Fact]
  public async Task MismatchedTerminalRunCannotReleaseAdmission()
  {
    var handler = Handler(
        Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        new RecordedResponse(SucceededRun.Replace(AdmittedRunId, "00000000-0000-7000-8000-000000000999", StringComparison.Ordinal)));
    var model = await Loaded(handler, 1);
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.True(model.IsJudgementPending(ReportId));
    Assert.Empty(model.RowErrors);
  }

  [Fact]
  public async Task AmbiguousAdmissionFailureBlocksAnotherPost()
  {
    var handler = Handler(Fixture("native.moderation.reports.default"), new RecordedResponse("{}", HttpStatusCode.InternalServerError));
    var model = await Loaded(handler, 1);
    Assert.False(await model.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.True(model.IsJudgementPending(ReportId));
    Assert.False(model.HasAdmittedJudgementRun(ReportId));
    Assert.False(await model.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.False(await model.RefreshPendingJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.Single(Posts(handler));
    Assert.Empty(RunGets(handler));
  }

  [Fact]
  public async Task ReopenedOwnerRetainsExactRunWhileOtherOwnerCannotReadIt()
  {
    var store = new ReportJudgementAdmissionStore();
    var handler = Handler(Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture("native.moderation.report-judgement-run.pending"));
    var first = ViewModel(handler, 1, _ => Task.CompletedTask, store, "alice");
    await LoadFlat(first);
    Assert.True(await first.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    var other = ViewModel(Handler(Fixture("native.moderation.reports.default")), 1, _ => Task.CompletedTask, store, "bob");
    await LoadFlat(other);
    Assert.False(other.IsJudgementPending(ReportId));
    Assert.False(await other.RefreshPendingJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    var reopenedHandler = Handler(Fixture("native.moderation.reports.default"), Fixture("native.moderation.report-judgement-run.failed"));
    var reopened = ViewModel(reopenedHandler, 1, _ => Task.CompletedTask, store, "alice");
    await LoadFlat(reopened);
    Assert.True(reopened.IsJudgementPending(ReportId));
    Assert.False(await reopened.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.False(await reopened.RefreshPendingJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.False(first.IsJudgementPending(ReportId));
    Assert.Empty(Posts(reopenedHandler));
    Assert.False(other.IsJudgementPending(ReportId));
  }

  [Fact]
  public async Task DisposedOwnerCannotReleaseAdmissionWithLateTerminalRead()
  {
    var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var store = new ReportJudgementAdmissionStore();
    var handler = Handler(Fixture("native.moderation.reports.default"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture("native.moderation.report-judgement-run.pending"),
        Fixture("native.moderation.report-judgement-run.failed"));
    var original = ViewModel(handler, 2, _ => { paused.SetResult(); return resume.Task; }, store, "alice");
    await LoadFlat(original);
    var running = original.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken);
    await paused.Task;
    var other = ViewModel(Handler(Fixture("native.moderation.reports.default")), 1, _ => Task.CompletedTask, store, "bob");
    await LoadFlat(other);
    original.Dispose();
    resume.SetResult();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    Assert.True(original.IsJudgementPending(ReportId));
    Assert.False(other.IsJudgementPending(ReportId));
    Assert.Empty(other.RowErrors);
  }

  [Theory]
  [InlineData(HttpStatusCode.BadRequest, false)]
  [InlineData(HttpStatusCode.Unauthorized, false)]
  [InlineData(HttpStatusCode.Forbidden, false)]
  [InlineData(HttpStatusCode.NotFound, false)]
  [InlineData(HttpStatusCode.RequestTimeout, true)]
  [InlineData(HttpStatusCode.InternalServerError, true)]
  public async Task KnownRejectionsReleaseWhileAmbiguousDeliveryRetainsAdmission(HttpStatusCode status, bool pending)
  {
    var model = await Loaded(Handler(Fixture("native.moderation.reports.default"), new RecordedResponse("{}", status)), 1);
    Assert.False(await model.RerunJudgementAsync(ReportId, TestContext.Current.CancellationToken));
    Assert.Equal(pending, model.IsJudgementPending(ReportId));
  }

  private static Task LoadFlat(ModerationReportsViewModel model) => model.SetFiltersAsync(
      ModerationReportStatus.Pending, ModerationReportsMode.Flat, ModerationReportSort.CreatedAtDesc, TestContext.Current.CancellationToken);

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
      Func<CancellationToken, Task> delay,
      ReportJudgementAdmissionStore? store = null, string? identity = null) =>
      new(
          new ApiModerationService(new VouchaApiClient(
              new HttpClient(handler) { BaseAddress = new Uri("https://api.test") })),
          new NavigationViewer(true, ["administrator"], IdentityId: identity),
          judgementRunReads: reads,
          judgementRunDelay: delay, judgementAdmissions: store, apiBaseUrl: identity is null ? null : new Uri("https://api.test"));

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

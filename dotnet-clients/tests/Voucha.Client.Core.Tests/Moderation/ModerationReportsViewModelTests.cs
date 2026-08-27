using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationReportsViewModelTests
{
  [Fact]
  public async Task StaffDefaultsToGroupedSeverityAndMembersLoadNewestRedactedFlat()
  {
    var staffHandler = Handler(Fixture("native.moderation.reports.clustered.default"));
    var staff = ViewModel(staffHandler, "moderator");
    await staff.LoadAsync(TestContext.Current.CancellationToken);

    var memberHandler = Handler(Fixture("native.moderation.reports.member.default"));
    var member = ViewModel(memberHandler);
    await member.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ModerationReportsMode.Grouped, staff.Mode);
    Assert.Equal(ModerationReportSort.Severity, staff.Sort);
    Assert.Equal(4, staff.Clusters.Count);
    Assert.Single(staff.DuplicateClusters);
    Assert.Equal("/api/v1/reports?cluster=entity&limit=25&status=pending", staffHandler.Requests.Single().PathAndQuery);
    Assert.Equal(ModerationReportsMode.Flat, member.Mode);
    Assert.Equal(ModerationReportSort.CreatedAtDesc, member.Sort);
    Assert.True(member.IsMemberReadOnly);
    Assert.Single(member.MemberReports);
    Assert.Equal("/api/v1/reports?limit=25&sort=created_at_desc&status=pending", memberHandler.Requests.Single().PathAndQuery);
  }

  [Fact]
  public async Task InitialClusterFailureFallsBackWithoutDiscardingNotice()
  {
    var handler = Handler(
        new RecordedResponse("{}", HttpStatusCode.InternalServerError),
        Fixture("native.moderation.reports.default"));
    var viewModel = ViewModel(handler, "moderator");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ModerationReportsMode.Flat, viewModel.Mode);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Single(viewModel.StaffReports);
    Assert.Contains("Showing the report list", viewModel.Notice, StringComparison.Ordinal);
    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal("/api/v1/reports?cluster=entity&limit=25&status=pending", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/reports?limit=25&sort=severity&status=pending", handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task SwitchingFromGroupedToFlatKeepsStaffSeverityDefault()
  {
    var handler = Handler(Fixture("native.moderation.reports.default"));
    var viewModel = ViewModel(handler, "moderator");

    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Pending,
        ModerationReportsMode.Flat,
        viewModel.Sort,
        TestContext.Current.CancellationToken);

    Assert.Equal(ModerationReportsMode.Flat, viewModel.Mode);
    Assert.Equal(ModerationReportSort.Severity, viewModel.Sort);
    Assert.Equal("/api/v1/reports?limit=25&sort=severity&status=pending", handler.Requests.Single().PathAndQuery);
  }

  [Fact]
  public async Task FilterChangesRejectStaleLoadsAndResetPagingAndSelection()
  {
    var handler = new CancelThenRespondHandler(Fixture("native.moderation.reports.default"));
    var viewModel = ViewModel(handler, "moderator");
    viewModel.ToggleSelection("stale-selection");
    Assert.True(viewModel.IsSelected("stale-selection"));
    var staleLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await handler.FirstRequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Reviewed,
        ModerationReportsMode.Flat,
        ModerationReportSort.MostReported,
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => staleLoad);
    Assert.False(viewModel.IsSelected("stale-selection"));
    Assert.Equal(ModerationReportStatus.Reviewed, viewModel.Status);
    Assert.Equal(ModerationReportSort.MostReported, viewModel.Sort);
    Assert.Single(viewModel.StaffReports);
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task PaginationAppendsDedupesAndPreservesLoadedStateOnFailure()
  {
    var first = WithPageInfo(FixtureBody("native.moderation.reports.default"), "cursor-1", true);
    var second = WithAdditionalReport(FixtureBody("native.moderation.reports.default"), "report-2", "cursor-2", true);
    var handler = Handler(
        new RecordedResponse(first),
        new RecordedResponse(second),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError));
    var viewModel = ViewModel(handler, "moderator");
    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Pending, ModerationReportsMode.Flat,
        ModerationReportSort.CreatedAtDesc, TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, viewModel.StaffReports.Count);
    Assert.Equal(2, viewModel.StaffReports.Select(report => report.Id).Distinct().Count());
    Assert.Equal("/api/v1/reports?after=cursor-1&limit=25&sort=created_at_desc&status=pending", handler.Requests[1].PathAndQuery);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, viewModel.StaffReports.Count);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.NotNull(viewModel.ErrorMessage);
    Assert.True(viewModel.HasNextPage);
  }

  [Fact]
  public async Task GroupedPaginationMergesRecurringDuplicateClusterSidecars()
  {
    var firstPage = FixtureBody("native.moderation.reports.clustered.default");
    var overlappingSecondPage = WithDuplicateClusterOverlap(
        firstPage, FixtureBody("native.moderation.reports.clustered.page-2"));
    var handler = Handler(
        new RecordedResponse(firstPage),
        new RecordedResponse(overlappingSecondPage),
        Fixture("native.moderation.report-resolution.reviewed"),
        new RecordedResponse(firstPage),
        new RecordedResponse(overlappingSecondPage));
    var viewModel = ViewModel(handler, "moderator");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    var duplicate = Assert.Single(viewModel.DuplicateClusters);
    Assert.Equal(6, duplicate.Clusters.Count);
    Assert.Equal(6, duplicate.Clusters.Select(cluster => cluster.Id).Distinct().Count());
    Assert.Equal(6, duplicate.PostCount);
    Assert.Equal(10, duplicate.ReportCount);
    var reason = Assert.Single(duplicate.ReasonBreakdown);
    Assert.Equal("spam", reason.Reason);
    Assert.Equal(10, reason.Count);
    Assert.Equal(duplicate.Clusters.Min(cluster => cluster.FirstReportedAt), duplicate.FirstReportedAt);
    Assert.Equal(duplicate.Clusters.Max(cluster => cluster.LastReportedAt), duplicate.LastReportedAt);
    Assert.False(viewModel.HasNextPage);

    var reportId = viewModel.Clusters.SelectMany(cluster => cluster.Reports)
        .First(viewModel.CanResolve).Id;
    Assert.True(await viewModel.DismissAsync(reportId, TestContext.Current.CancellationToken));
    duplicate = Assert.Single(viewModel.DuplicateClusters);
    Assert.Equal(6, duplicate.Clusters.Count);
    Assert.Equal(6, duplicate.Clusters.Select(cluster => cluster.Id).Distinct().Count());
    Assert.Equal(6, duplicate.PostCount);
    Assert.Equal(10, duplicate.ReportCount);
    Assert.Single(duplicate.ReasonBreakdown);
    Assert.Equal(5, handler.Requests.Count);
  }

  [Fact]
  public async Task OrdinaryAndBanEvasionActionsUseDistinctEndpoints()
  {
    var reports = StaffReports(
        Ordinary("review", "post-review"),
        Ordinary("rerun", "post-rerun"),
        Ordinary("warning", "post-warning"),
        BanEvasion("confirm", "user-confirm"),
        BanEvasion("dismiss", "user-dismiss"));
    var handler = Handler(
        new RecordedResponse(reports),
        Fixture("native.moderation.report-resolution.reviewed"),
        Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
        Fixture("native.moderation.admin-warning.report", HttpStatusCode.Created),
        new RecordedResponse("null", HttpStatusCode.NoContent),
        new RecordedResponse("null", HttpStatusCode.NoContent));
    var viewModel = ViewModel(handler, "administrator");
    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Pending, ModerationReportsMode.Flat,
        ModerationReportSort.CreatedAtDesc, TestContext.Current.CancellationToken);

    Assert.True(await viewModel.ReviewAsync("review", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.RerunJudgementAsync("rerun", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.IssueWarningAsync("warning", "Reason", "Message", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.ConfirmBanEvasionAsync("confirm", TestContext.Current.CancellationToken));
    Assert.True(await viewModel.DismissBanEvasionAsync("dismiss", TestContext.Current.CancellationToken));

    Assert.Equal(HttpMethod.Patch, handler.Requests[1].Method);
    Assert.EndsWith("/judgements", handler.Requests[2].PathAndQuery, StringComparison.Ordinal);
    Assert.Equal("/api/v1/admin/warnings", handler.Requests[3].PathAndQuery);
    Assert.Equal(HttpMethod.Post, handler.Requests[4].Method);
    Assert.Equal(HttpMethod.Delete, handler.Requests[5].Method);
    Assert.Empty(viewModel.RowErrors);
  }

  [Fact]
  public async Task BulkActionsSettleIndependentlyAndKeepFailedOrSkippedSelected()
  {
    var reports = StaffReports(
        Ordinary("same-1", "post-same"),
        Ordinary("same-2", "post-same"),
        Ordinary("failed", "post-failed"),
        BanEvasion("skipped", "user-skipped"));
    var handler = Handler(
        new RecordedResponse(reports),
        new RecordedResponse("null", HttpStatusCode.NoContent),
        new RecordedResponse("{}", HttpStatusCode.InternalServerError));
    var viewModel = ViewModel(handler, "administrator");
    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Pending, ModerationReportsMode.Flat,
        ModerationReportSort.CreatedAtDesc, TestContext.Current.CancellationToken);
    foreach (var id in new[] { "same-1", "same-2", "failed", "skipped" }) viewModel.ToggleSelection(id);

    var result = await viewModel.RemoveSelectedTargetsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new ModerationBulkResult(2, 1, 1), result);
    Assert.Equal(2, handler.Requests.Skip(1).Count());
    Assert.False(viewModel.IsSelected("same-1"));
    Assert.False(viewModel.IsSelected("same-2"));
    Assert.True(viewModel.IsSelected("failed"));
    Assert.True(viewModel.IsSelected("skipped"));
    Assert.Contains("failed", viewModel.RowErrors.Keys);
  }

  [Fact]
  public async Task UnexpectedGroupedRefreshFailureKeepsSuccessfulRowTombstoned()
  {
    var handler = new UnexpectedRefreshHandler(
        Fixture("native.moderation.reports.clustered.default"),
        Fixture("native.moderation.report-resolution.reviewed"));
    var viewModel = ViewModel(handler, "administrator");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(await viewModel.DismissAsync("00000000-0000-7000-8100-000000000101", TestContext.Current.CancellationToken));

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("refresh exploded", viewModel.ErrorMessage);
    Assert.Equal("Reports changed. Refresh the queue to see current totals.", viewModel.Notice);
    Assert.Null(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000101"));
  }

  private static ModerationReportsViewModel ViewModel(RecordingHandler handler, params string[] roles) =>
      new(new ApiModerationService(Client(handler)), new NavigationViewer(true, roles));

  private static ModerationReportsViewModel ViewModel(HttpMessageHandler handler, params string[] roles) =>
      new(new ApiModerationService(Client(handler)), new NavigationViewer(true, roles));

  private static VouchaApiClient Client(HttpMessageHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private static RecordingHandler Handler(params RecordedResponse[] responses) => new(responses);
  private static RecordedResponse Fixture(string id, HttpStatusCode status = HttpStatusCode.OK) =>
      new(FixtureBody(id), status);
  private static string FixtureBody(string id) => ApiFixtureLoader.LoadResponse(id);

  private static string WithPageInfo(string json, string cursor, bool hasNext)
  {
    var root = JsonNode.Parse(json)!.AsObject();
    root["page_info"] = new JsonObject { ["end_cursor"] = cursor, ["has_next_page"] = hasNext };
    return root.ToJsonString();
  }

  private static string WithAdditionalReport(string json, string id, string cursor, bool hasNext)
  {
    var root = JsonNode.Parse(json)!.AsObject();
    var reports = root["results"]!.AsArray();
    var copy = reports[0]!.DeepClone().AsObject();
    copy["id"] = id;
    reports.Add(copy);
    root["page_info"] = new JsonObject { ["end_cursor"] = cursor, ["has_next_page"] = hasNext };
    return root.ToJsonString();
  }

  private static string WithDuplicateClusterOverlap(string firstPage, string secondPage)
  {
    var firstRoot = JsonNode.Parse(firstPage)!.AsObject();
    var secondRoot = JsonNode.Parse(secondPage)!.AsObject();
    var overlap = firstRoot["duplicate_clusters"]![0]!["clusters"]![0]!.DeepClone();
    secondRoot["duplicate_clusters"]![0]!["clusters"]!.AsArray().Add(overlap);
    return secondRoot.ToJsonString();
  }

  private static JsonObject Ordinary(string id, string entityId)
  {
    var report = BaseReport(id, entityId);
    report["target_user_id"] = $"user-{id}";
    return report;
  }

  private static JsonObject BanEvasion(string id, string entityId)
  {
    var report = BaseReport(id, entityId);
    report["entity_type"] = "user";
    report["is_system_generated"] = true;
    report["community_ban_evasion"] = new JsonObject
    {
      ["community_id"] = "community-1",
      ["community_slug"] = "community",
      ["source_user_id"] = "source-1",
      ["source_username"] = "source",
      ["score"] = 0.9,
      ["flagged_at"] = "2026-06-01T12:00:00.000Z",
    };
    return report;
  }

  private static JsonObject BaseReport(string id, string entityId)
  {
    var root = JsonNode.Parse(FixtureBody("native.moderation.reports.default"))!.AsObject();
    var report = root["results"]![0]!.DeepClone().AsObject();
    report["id"] = id;
    report["entity_id"] = entityId;
    return report;
  }

  private static string StaffReports(params JsonObject[] reports) => new JsonObject
  {
    ["results"] = new JsonArray(reports.Cast<JsonNode?>().ToArray()),
    ["page_info"] = new JsonObject { ["end_cursor"] = null, ["has_next_page"] = false },
  }.ToJsonString();

  private sealed class CancelThenRespondHandler(RecordedResponse secondResponse) : HttpMessageHandler
  {
    private int requestCount;
    public TaskCompletionSource FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Requests.Add(new RecordedRequest(request.Method, request.RequestUri?.PathAndQuery, null));
      if (Interlocked.Increment(ref requestCount) == 1)
      {
        FirstRequestStarted.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      }
      return new HttpResponseMessage(secondResponse.StatusCode)
      {
        Content = new StringContent(secondResponse.Body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }
  }

  private sealed class UnexpectedRefreshHandler(
      RecordedResponse initial,
      RecordedResponse resolution) : HttpMessageHandler
  {
    private int requestCount;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var response = Interlocked.Increment(ref requestCount) switch
      {
        1 => initial,
        2 => resolution,
        _ => throw new NotSupportedException("refresh exploded"),
      };
      return Task.FromResult(new HttpResponseMessage(response.StatusCode)
      {
        Content = new StringContent(response.Body),
        RequestMessage = request,
      });
    }
  }
}

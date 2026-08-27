using System.Net;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationReportsRemediationTests
{
  private const string DuplicateClusterId =
      "content-hash:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
  private const string Post1ClusterId = "post:00000000-0000-7000-8000-000000000101";
  private const string Post2ClusterId = "post:00000000-0000-7000-8000-000000000102";
  private const string Post3ClusterId = "post:00000000-0000-7000-8000-000000000103";
  private const string UserClusterId = "user:00000000-0000-7000-8000-000000000201";

  [Theory]
  [InlineData("reviewed")]
  [InlineData("actioned")]
  [InlineData("dismissed")]
  public async Task OnlyJudgementRerunIsAllowedAfterAReportLeavesPending(string status)
  {
    var reports = StaffReports(
        WithStatus(Ordinary("ordinary", "post-1"), status),
        WithStatus(BanEvasion("ban-evasion", "user-1"), status));
    var handler = new RecordingHandler([
      new RecordedResponse(reports),
      Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
    ]);
    var viewModel = ViewModel(handler, "administrator");
    await LoadFlatAsync(viewModel);
    var token = TestContext.Current.CancellationToken;

    Assert.False(await viewModel.ReviewAsync("ordinary", token));
    Assert.False(await viewModel.DismissAsync("ordinary", token));
    Assert.False(await viewModel.IssueWarningAsync("ordinary", "reason", null, token));
    Assert.False(await viewModel.RemoveTargetAsync("ordinary", token));
    Assert.False(await viewModel.ConfirmBanEvasionAsync("ban-evasion", token));
    Assert.False(await viewModel.DismissBanEvasionAsync("ban-evasion", token));
    Assert.True(await viewModel.RerunJudgementAsync("ordinary", token));
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task PendingActionEligibilityEnforcesModeratorAndAdministratorRoles()
  {
    var body = StaffReports(Ordinary("ordinary", "post-1"), BanEvasion("ban-evasion", "user-1"));
    var moderator = ViewModel(new RecordingHandler([new RecordedResponse(body)]), "moderator");
    var administrator = ViewModel(new RecordingHandler([new RecordedResponse(body)]), "administrator");
    var memberHandler = new RecordingHandler([Fixture("native.moderation.reports.member.default")]);
    var member = ViewModel(memberHandler);
    await LoadFlatAsync(moderator);
    await LoadFlatAsync(administrator);
    await member.LoadAsync(TestContext.Current.CancellationToken);

    var moderatorOrdinary = moderator.FindStaffReport("ordinary");
    var moderatorBanEvasion = moderator.FindStaffReport("ban-evasion");
    Assert.True(moderator.CanResolve(moderatorOrdinary));
    Assert.True(moderator.CanIssueWarning(moderatorOrdinary));
    Assert.True(moderator.CanHandleBanEvasion(moderatorBanEvasion));
    Assert.False(moderator.CanRemove(moderatorOrdinary));
    Assert.True(administrator.CanRemove(administrator.FindStaffReport("ordinary")));
    Assert.False(member.CanResolve(null));
    Assert.False(await member.RerunJudgementAsync("ordinary", TestContext.Current.CancellationToken));
    Assert.Single(memberHandler.Requests);
  }

  [Fact]
  public async Task ClusterEligibilityExcludesResolvedAndSystemOnlyReports()
  {
    var body = SetReportSystemGenerated(
        SetReportStatus(FixtureBody("native.moderation.reports.clustered.default"), "00000000-0000-7000-8100-000000000101", "reviewed"),
        "00000000-0000-7000-8100-000000000201");
    var viewModel = ViewModel(new RecordingHandler([new RecordedResponse(body)]), "administrator");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanDismissCluster(Post1ClusterId));
    Assert.False(viewModel.CanRemoveClusterTargets(Post1ClusterId));
    Assert.False(viewModel.CanDismissCluster(UserClusterId));
    Assert.False(viewModel.CanRemoveClusterTargets(UserClusterId));
    Assert.True(viewModel.CanDismissCluster(DuplicateClusterId));
    Assert.True(viewModel.CanRemoveClusterTargets(DuplicateClusterId));
  }

  [Fact]
  public async Task DuplicateBulkActionRefreshesServerAggregatesAndPreservesFailedAndSkippedState()
  {
    var initial = SetReportStatus(
        FixtureBody("native.moderation.reports.clustered.default"), "00000000-0000-7000-8100-000000000103", "reviewed");
    var refreshed = OmitReportAndStrengthenTotals(initial, "00000000-0000-7000-8100-000000000101");
    var handler = new RecordingHandler([
      new RecordedResponse(initial),
      Fixture("native.moderation.report-resolution.reviewed"),
      new RecordedResponse("{}", HttpStatusCode.InternalServerError),
      new RecordedResponse(refreshed),
    ]);
    var viewModel = ViewModel(handler, "administrator");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var result = await viewModel.DismissClusterAsync(
        DuplicateClusterId, TestContext.Current.CancellationToken);

    Assert.Equal(new ModerationBulkResult(1, 1, 1), result);
    Assert.False(viewModel.IsSelected("00000000-0000-7000-8100-000000000101"));
    Assert.True(viewModel.IsSelected("00000000-0000-7000-8100-000000000102"));
    Assert.True(viewModel.IsSelected("00000000-0000-7000-8100-000000000103"));
    Assert.Contains("00000000-0000-7000-8100-000000000102", viewModel.RowErrors.Keys);
    Assert.Equal("Only pending reports can be dismissed.", viewModel.RowErrors["00000000-0000-7000-8100-000000000103"]);
    Assert.Null(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000101"));
    var duplicate = Assert.Single(viewModel.DuplicateClusters);
    Assert.Equal(8, duplicate.ReportCount);
    Assert.Equal(8, Assert.Single(duplicate.ReasonBreakdown).Count);
    Assert.Equal(4, duplicate.Clusters[0].ReportCount);
    Assert.Contains(duplicate.Clusters, cluster => cluster.Id == Post2ClusterId);
    Assert.Equal(4, handler.Requests.Count);
    Assert.Equal("/api/v1/reports?cluster=entity&limit=25&status=pending", handler.Requests[^1].PathAndQuery);
  }

  [Fact]
  public async Task GroupedMutationsAndRefreshAreQueueWideSerialized()
  {
    var initial = FixtureBody("native.moderation.reports.clustered.default");
    var handler = new BlockingMutationHandler(
        initial, OmitReportAndStrengthenTotals(initial, "00000000-0000-7000-8100-000000000101"));
    var viewModel = ViewModel(handler, "administrator");
    var token = TestContext.Current.CancellationToken;
    await viewModel.LoadAsync(token);

    var firstMutation = viewModel.DismissAsync("00000000-0000-7000-8100-000000000101", token);
    await handler.MutationStarted.WaitAsync(token);

    var queueWasMutationBlocked = viewModel.IsQueueMutationRunning;
    var secondMutation = await viewModel.DismissAsync("00000000-0000-7000-8100-000000000102", token);
    var clusterMutation = await viewModel.DismissClusterAsync(Post2ClusterId, token);
    await viewModel.LoadAsync(token);
    var requestCountBeforeRelease = handler.RequestCount;
    var selectionBeforeRelease = viewModel.SelectedIds.ToArray();

    handler.ReleaseMutation();
    Assert.True(queueWasMutationBlocked);
    Assert.False(secondMutation);
    Assert.Equal(new ModerationBulkResult(0, 0, 1), clusterMutation);
    Assert.Empty(selectionBeforeRelease);
    Assert.Equal(2, requestCountBeforeRelease);
    Assert.True(await firstMutation);
    Assert.False(viewModel.IsQueueMutationRunning);
    Assert.Equal(3, handler.RequestCount);
    Assert.Null(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000101"));
    Assert.True(viewModel.CanResolve(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000102")));
  }

  [Fact]
  public async Task TwoPageDuplicateRefreshPreservesDepthAndLaterRetryableRows()
  {
    var fixture = SetReportStatus(
        FixtureBody("native.moderation.reports.clustered.default"), "00000000-0000-7000-8100-000000000103", "reviewed");
    var initialPage1 = GroupedPage(fixture, [UserClusterId], false, "cursor-1", true);
    var initialPage2 = GroupedPage(
        fixture, [Post1ClusterId, Post2ClusterId, Post3ClusterId], true, "cursor-2", true);
    var refreshedPage1 = GroupedPage(fixture, [UserClusterId], false, "refresh-1", true);
    var refreshedPage2 = GroupedPage(
        OmitReportAndStrengthenTotals(fixture, "00000000-0000-7000-8100-000000000101"),
        [Post2ClusterId, Post3ClusterId], true, "refresh-2", true);
    var handler = new RecordingHandler([
      new RecordedResponse(initialPage1),
      new RecordedResponse(initialPage2),
      Fixture("native.moderation.report-resolution.reviewed"),
      new RecordedResponse("{}", HttpStatusCode.InternalServerError),
      new RecordedResponse(refreshedPage1),
      new RecordedResponse(refreshedPage2),
      new RecordedResponse(GroupedPage(fixture, [], false, null, false)),
    ]);
    var viewModel = ViewModel(handler, "administrator");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    var result = await viewModel.DismissClusterAsync(
        DuplicateClusterId, TestContext.Current.CancellationToken);

    Assert.Equal(new ModerationBulkResult(1, 1, 1), result);
    Assert.Null(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000101"));
    Assert.True(viewModel.CanResolve(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000102")));
    Assert.NotNull(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000103"));
    Assert.True(viewModel.IsSelected("00000000-0000-7000-8100-000000000102"));
    Assert.True(viewModel.IsSelected("00000000-0000-7000-8100-000000000103"));
    Assert.Contains("00000000-0000-7000-8100-000000000102", viewModel.RowErrors.Keys);
    var duplicate = Assert.Single(viewModel.DuplicateClusters);
    Assert.Equal(8, duplicate.ReportCount);
    Assert.Equal(8, Assert.Single(duplicate.ReasonBreakdown).Count);
    Assert.DoesNotContain(
        duplicate.Clusters.SelectMany(cluster => cluster.Reports), report => report.Id == "00000000-0000-7000-8100-000000000101");
    Assert.Equal("/api/v1/reports?after=refresh-1&cluster=entity&limit=25&status=pending", handler.Requests[5].PathAndQuery);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/reports?after=refresh-2&cluster=entity&limit=25&status=pending", handler.Requests[6].PathAndQuery);
  }

  [Fact]
  public async Task FailedTwoPageRefreshPreservesOldRowsCursorAndRetryableState()
  {
    var fixture = SetReportStatus(
        FixtureBody("native.moderation.reports.clustered.default"), "00000000-0000-7000-8100-000000000103", "reviewed");
    var handler = new RecordingHandler([
      new RecordedResponse(GroupedPage(fixture, [UserClusterId], false, "cursor-1", true)),
      new RecordedResponse(GroupedPage(
          fixture, [Post1ClusterId, Post2ClusterId, Post3ClusterId], true, "cursor-2", true)),
      Fixture("native.moderation.report-resolution.reviewed"),
      new RecordedResponse("{}", HttpStatusCode.InternalServerError),
      new RecordedResponse("{}", HttpStatusCode.InternalServerError),
      new RecordedResponse(GroupedPage(fixture, [], false, null, false)),
    ]);
    var viewModel = ViewModel(handler, "administrator");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    var result = await viewModel.DismissClusterAsync(
        DuplicateClusterId, TestContext.Current.CancellationToken);

    Assert.Equal(new ModerationBulkResult(1, 1, 1), result);
    Assert.Null(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000101"));
    Assert.True(viewModel.CanResolve(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000102")));
    Assert.NotNull(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000103"));
    Assert.True(viewModel.IsSelected("00000000-0000-7000-8100-000000000102"));
    Assert.True(viewModel.IsSelected("00000000-0000-7000-8100-000000000103"));
    Assert.Contains("00000000-0000-7000-8100-000000000102", viewModel.RowErrors.Keys);
    Assert.Single(viewModel.DuplicateClusters);
    var visibleReportIds = viewModel.ReportsForCluster(DuplicateClusterId)
        .Select(report => report.Id).ToArray();
    Assert.DoesNotContain("00000000-0000-7000-8100-000000000101", visibleReportIds);
    Assert.Contains("00000000-0000-7000-8100-000000000102", visibleReportIds);
    Assert.Contains("00000000-0000-7000-8100-000000000103", visibleReportIds);
    var staleDuplicate = Assert.Single(viewModel.DuplicateClusters);
    var removedShell = Assert.Single(staleDuplicate.Clusters, cluster => cluster.Id == Post1ClusterId);
    var removedWave = staleDuplicate with { Clusters = [removedShell] };
    var cappedActiveShell = Assert.Single(
        staleDuplicate.Clusters, cluster => cluster.Id == Post2ClusterId)
    with
    {
      ReportCount = 25,
    };
    Assert.Empty(viewModel.ReportsForCluster(removedShell));
    Assert.Equal(25, viewModel.PresentationReportCount(cappedActiveShell));
    Assert.Equal(0, viewModel.PresentationReportCount(removedShell with { ReportCount = 25 }));
    Assert.False(viewModel.HasActiveReports(removedWave));
    Assert.Equal((0, 0), viewModel.PresentationCounts(removedWave));
    Assert.Empty(viewModel.PresentationReasons(removedWave));
    Assert.Equal((2, 2), viewModel.PresentationCounts(staleDuplicate));
    Assert.Equal(2, Assert.Single(viewModel.PresentationReasons(staleDuplicate)).Count);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("/api/v1/reports?after=cursor-2&cluster=entity&limit=25&status=pending", handler.Requests[5].PathAndQuery);
  }

  [Fact]
  public async Task CanceledGroupedRefreshRestoresLoadedStateAndShowsNotice()
  {
    using var cancellation = new CancellationTokenSource();
    var handler = new CancelRefreshHandler(
        Fixture("native.moderation.reports.clustered.default"),
        Fixture("native.moderation.report-resolution.reviewed"),
        cancellation);
    var viewModel = ViewModel(handler, "administrator");
    await viewModel.LoadAsync(cancellation.Token);

    var result = await viewModel.DismissClusterAsync(Post1ClusterId, cancellation.Token);

    Assert.Equal(new ModerationBulkResult(1, 0, 0), result);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("Reports changed. Refresh the queue to see current totals.", viewModel.Notice);
    Assert.Null(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000101"));
    Assert.Empty(viewModel.ReportsForCluster(Post1ClusterId));
    Assert.Contains(
        viewModel.Clusters.SelectMany(cluster => cluster.Reports), report => report.Id == "00000000-0000-7000-8100-000000000101");

    await viewModel.SetFiltersAsync(
        ModerationReportStatus.Reviewed,
        ModerationReportsMode.Grouped,
        ModerationReportSort.CreatedAtDesc,
        TestContext.Current.CancellationToken);
    Assert.NotNull(viewModel.FindStaffReport("00000000-0000-7000-8100-000000000101"));
  }

  private static async Task LoadFlatAsync(ModerationReportsViewModel viewModel) =>
      await viewModel.SetFiltersAsync(
          ModerationReportStatus.Pending,
          ModerationReportsMode.Flat,
          ModerationReportSort.Severity,
          TestContext.Current.CancellationToken);

  private static ModerationReportsViewModel ViewModel(RecordingHandler handler, params string[] roles) =>
      new(new ApiModerationService(new VouchaApiClient(
          new HttpClient(handler) { BaseAddress = new Uri("https://api.test") })),
          new NavigationViewer(true, roles));

  private static ModerationReportsViewModel ViewModel(HttpMessageHandler handler, params string[] roles) =>
      new(new ApiModerationService(new VouchaApiClient(
          new HttpClient(handler) { BaseAddress = new Uri("https://api.test") })),
          new NavigationViewer(true, roles));

  private static RecordedResponse Fixture(string id, HttpStatusCode status = HttpStatusCode.OK) =>
      new(FixtureBody(id), status);

  private static string FixtureBody(string id) => ApiFixtureLoader.LoadResponse(id);

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

  private static JsonObject WithStatus(JsonObject report, string status)
  {
    report["status"] = status;
    return report;
  }

  private static string StaffReports(params JsonObject[] reports) => new JsonObject
  {
    ["results"] = new JsonArray(reports.Cast<JsonNode?>().ToArray()),
    ["page_info"] = new JsonObject { ["end_cursor"] = null, ["has_next_page"] = false },
  }.ToJsonString();

  private static string SetReportStatus(string json, string reportId, string status)
  {
    var root = JsonNode.Parse(json)!.AsObject();
    foreach (var cluster in root["results"]!.AsArray().Select(node => node!.AsObject()))
      SetNestedReportStatus(cluster, reportId, status);
    foreach (var duplicate in root["duplicate_clusters"]!.AsArray().Select(node => node!.AsObject()))
      foreach (var cluster in duplicate["clusters"]!.AsArray().Select(node => node!.AsObject()))
        SetNestedReportStatus(cluster, reportId, status);
    return root.ToJsonString();
  }

  private static string SetReportSystemGenerated(string json, string reportId)
  {
    var root = JsonNode.Parse(json)!.AsObject();
    foreach (var cluster in root["results"]!.AsArray().Select(node => node!.AsObject()))
      SetNestedSystemGenerated(cluster, reportId);
    foreach (var duplicate in root["duplicate_clusters"]!.AsArray().Select(node => node!.AsObject()))
      foreach (var cluster in duplicate["clusters"]!.AsArray().Select(node => node!.AsObject()))
        SetNestedSystemGenerated(cluster, reportId);
    return root.ToJsonString();
  }

  private static void SetNestedSystemGenerated(JsonObject cluster, string reportId)
  {
    foreach (var report in cluster["reports"]!.AsArray().Select(node => node!.AsObject()))
      if (report["id"]?.GetValue<string>() == reportId) report["is_system_generated"] = true;
  }

  private static void SetNestedReportStatus(JsonObject cluster, string reportId, string status)
  {
    foreach (var report in cluster["reports"]!.AsArray().Select(node => node!.AsObject()))
      if (report["id"]?.GetValue<string>() == reportId) report["status"] = status;
  }

  private static string GroupedPage(
      string json,
      string[] clusterIds,
      bool includeDuplicate,
      string? endCursor,
      bool hasNextPage)
  {
    var root = JsonNode.Parse(json)!.AsObject();
    var clusters = root["results"]!.AsArray();
    foreach (var cluster in clusters.ToArray())
      if (!clusterIds.Contains(cluster!["id"]!.GetValue<string>())) clusters.Remove(cluster);
    if (!includeDuplicate) root["duplicate_clusters"] = new JsonArray();
    root["page_info"] = new JsonObject
    {
      ["end_cursor"] = endCursor,
      ["has_next_page"] = hasNextPage,
    };
    return root.ToJsonString();
  }

  private static string OmitReportAndStrengthenTotals(string json, string reportId)
  {
    var root = JsonNode.Parse(json)!.AsObject();
    foreach (var cluster in root["results"]!.AsArray().Select(node => node!.AsObject()))
      RemoveNestedReport(cluster, reportId);
    var duplicate = root["duplicate_clusters"]![0]!.AsObject();
    foreach (var cluster in duplicate["clusters"]!.AsArray().Select(node => node!.AsObject()).ToArray())
    {
      RemoveNestedReport(cluster, reportId);
      if (cluster["reports"]!.AsArray().Count == 0) duplicate["clusters"]!.AsArray().Remove(cluster);
    }
    var retainedCluster = duplicate["clusters"]![0]!.AsObject();
    retainedCluster["report_count"] = 4;
    retainedCluster["reason_breakdown"]![0]!["count"] = 4;
    duplicate["report_count"] = 8;
    duplicate["reason_breakdown"]![0]!["count"] = 8;
    return root.ToJsonString();
  }

  private static void RemoveNestedReport(JsonObject cluster, string reportId)
  {
    var reports = cluster["reports"]!.AsArray();
    foreach (var report in reports.ToArray())
      if (report!["id"]!.GetValue<string>() == reportId) reports.Remove(report);
  }

  private sealed class CancelRefreshHandler(
      RecordedResponse initial,
      RecordedResponse resolution,
      CancellationTokenSource cancellation) : HttpMessageHandler
  {
    private int requestCount;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      requestCount++;
      if (requestCount == 3)
      {
        cancellation.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
      }
      var response = requestCount switch
      {
        1 => initial,
        4 => new RecordedResponse(SetReportStatus(initial.Body, "00000000-0000-7000-8100-000000000101", "reviewed")),
        _ => resolution,
      };
      return Task.FromResult(new HttpResponseMessage(response.StatusCode)
      {
        Content = new StringContent(response.Body),
        RequestMessage = request,
      });
    }
  }

  private sealed class BlockingMutationHandler(string initial, string refreshed) : HttpMessageHandler
  {
    private readonly TaskCompletionSource<bool> mutationStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> mutationRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int requestCount;

    public Task MutationStarted => mutationStarted.Task;
    public int RequestCount => requestCount;
    public void ReleaseMutation() => mutationRelease.TrySetResult(true);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var current = Interlocked.Increment(ref requestCount);
      if (current == 2)
      {
        mutationStarted.TrySetResult(true);
        await mutationRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
      var body = current switch
      {
        1 => initial,
        2 => FixtureBody("native.moderation.report-resolution.reviewed"),
        _ => refreshed,
      };
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body),
        RequestMessage = request,
      };
    }
  }
}

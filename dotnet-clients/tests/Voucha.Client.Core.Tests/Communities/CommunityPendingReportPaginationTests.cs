using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityPendingReportPaginationTests
{
  private const string FullReportId = "00000000-0000-7000-8000-000000000601";

  [Fact]
  public async Task PendingReportsAppendByCursorAndResolutionRemovesEveryProjection()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    service.ModerationQueueResponses.Enqueue(QueueResponse("report-1"));
    service.PendingReportResponses.Enqueue(PendingPage("report-1", "next", true));
    service.PendingReportResponses.Enqueue(PendingPage("report-2", null, false));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.Moderation,
        TestContext.Current.CancellationToken);

    await viewModel.LoadMorePendingReportsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["report-1", "report-2"], viewModel.PendingReportRows.Select(row => row.Id));
    Assert.Equal([null, "next"], service.PendingReportCursors);
    Assert.Contains(viewModel.ModerationRows, row => row.Id == "report-1");
    var summary = Assert.Single(viewModel.Moderation, row => row.Id == "report-1");
    Assert.Equal("محتوى مبلّغ عنه", summary.AuthoredContent);
    Assert.Equal("RightToLeft", summary.AuthoredContentFlowDirection);

    Assert.True(await viewModel.ResolveModerationReportAsync(
        "report-1", "resolved", TestContext.Current.CancellationToken));

    Assert.DoesNotContain(viewModel.PendingReportRows, row => row.Id == "report-1");
    Assert.DoesNotContain(viewModel.ModerationRows, row => row.Id == "report-1");
    Assert.DoesNotContain(viewModel.Moderation, row => row.Id == "report-1");
    Assert.Contains(viewModel.PendingReportRows, row => row.Id == "report-2");
  }

  [Fact]
  public async Task FailedContinuationPreservesRowsAndRetriesSameCursorBeforeTerminalDedupe()
  {
    var fullReport = SharedPendingReportsFixture().Reports.Single();
    var secondReport = fullReport with { Id = "report-2", TargetLabel = "Second target" };
    var (viewModel, service) = await CreateViewModelAsync(
        new CommunityPendingReportsResponse([fullReport], new PageInfo("same-cursor", true, null)));
    service.PendingReportRequests.Enqueue(_ => Task.FromException<CommunityPendingReportsResponse>(
        new InvalidOperationException("offline")));
    service.PendingReportRequests.Enqueue(_ => Task.FromResult(
        new CommunityPendingReportsResponse(
            [fullReport with { TargetLabel = "Stale duplicate" }, secondReport],
            new PageInfo("same-cursor", false, null))));

    await viewModel.LoadMorePendingReportsAsync(TestContext.Current.CancellationToken);

    Assert.Equal([FullReportId], viewModel.PendingReportRows.Select(row => row.Id));
    Assert.True(viewModel.HasPendingReportPaginationError);
    Assert.Equal("offline", viewModel.PendingReportPaginationErrorMessage);
    Assert.False(viewModel.CanAutomaticallyLoadPendingReports);

    await viewModel.LoadMorePendingReportsAsync(TestContext.Current.CancellationToken);

    Assert.Equal([FullReportId, "report-2"], viewModel.PendingReportRows.Select(row => row.Id));
    Assert.Equal("Fixture report target", viewModel.PendingReportRows[0].Target);
    Assert.Equal([null, "same-cursor", "same-cursor"], service.PendingReportCursors);
    Assert.False(viewModel.HasMorePendingReports);
    Assert.False(viewModel.HasPendingReportPaginationError);
    Assert.False(viewModel.CanAutomaticallyLoadPendingReports);

    await viewModel.LoadMorePendingReportsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3, service.PendingReportCursors.Count);
  }

  [Fact]
  public async Task CancelledContinuationPreservesRowsAndAllowsAutomaticRetry()
  {
    var fullReport = SharedPendingReportsFixture().Reports.Single();
    var (viewModel, service) = await CreateViewModelAsync(
        new CommunityPendingReportsResponse([fullReport], new PageInfo("retry-cursor", true, null)));
    service.PendingReportRequests.Enqueue(cancellationToken =>
        Task.FromCanceled<CommunityPendingReportsResponse>(cancellationToken));
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await viewModel.LoadMorePendingReportsAsync(cancellation.Token);

    Assert.Equal([FullReportId], viewModel.PendingReportRows.Select(row => row.Id));
    Assert.False(viewModel.IsLoadingMorePendingReports);
    Assert.False(viewModel.HasPendingReportPaginationError);
    Assert.True(viewModel.CanAutomaticallyLoadPendingReports);
  }

  [Fact]
  public async Task InitialReloadFailureSetsRetryStateAndRethrows()
  {
    var (viewModel, service) = await CreateViewModelAsync(PendingPage("report-1", "next", true));
    service.PendingReportRequests.Enqueue(_ => Task.FromException<CommunityPendingReportsResponse>(
        new InvalidOperationException("initial reload failed")));

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
        () => viewModel.LoadPendingReportsAsync(TestContext.Current.CancellationToken));

    Assert.Equal("initial reload failed", error.Message);
    Assert.Empty(viewModel.PendingReportRows);
    Assert.False(viewModel.IsLoadingMorePendingReports);
    Assert.True(viewModel.HasPendingReportPaginationError);
    Assert.False(viewModel.CanAutomaticallyLoadPendingReports);
  }

  [Fact]
  public async Task InitialReloadCancellationClearsLoadingAndRethrows()
  {
    var (viewModel, service) = await CreateViewModelAsync(PendingPage("report-1", "next", true));
    service.PendingReportRequests.Enqueue(cancellationToken =>
        Task.FromCanceled<CommunityPendingReportsResponse>(cancellationToken));
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => viewModel.LoadPendingReportsAsync(cancellation.Token));

    Assert.Empty(viewModel.PendingReportRows);
    Assert.False(viewModel.IsLoadingMorePendingReports);
    Assert.False(viewModel.HasPendingReportPaginationError);
    Assert.True(viewModel.CanAutomaticallyLoadPendingReports);
  }

  [Fact]
  public async Task StaleReloadCompletionCannotHideCurrentReloadFailure()
  {
    var (viewModel, service) = await CreateViewModelAsync(PendingPage("report-1", "next", true));
    var staleResponse = new TaskCompletionSource<CommunityPendingReportsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var currentResponse = new TaskCompletionSource<CommunityPendingReportsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var staleStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var currentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingReportRequests.Enqueue(_ =>
    {
      staleStarted.SetResult();
      return staleResponse.Task;
    });
    service.PendingReportRequests.Enqueue(_ =>
    {
      currentStarted.SetResult();
      return currentResponse.Task;
    });
    var staleReload = viewModel.LoadPendingReportsAsync(TestContext.Current.CancellationToken);
    await staleStarted.Task;
    var currentReload = viewModel.LoadPendingReportsAsync(TestContext.Current.CancellationToken);
    await currentStarted.Task;

    staleResponse.SetResult(PendingPage("stale-report", null, false));
    await staleReload;
    currentResponse.SetException(new InvalidOperationException("current reload failed"));
    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => currentReload);

    Assert.Equal("current reload failed", error.Message);
    Assert.Empty(viewModel.PendingReportRows);
    Assert.False(viewModel.IsLoadingMorePendingReports);
    Assert.True(viewModel.HasPendingReportPaginationError);
    Assert.Equal("current reload failed", viewModel.PendingReportPaginationErrorMessage);
  }

  [Fact]
  public async Task StaleContinuationCannotRepopulateAChangedSection()
  {
    var (viewModel, service) = await CreateViewModelAsync(PendingPage("report-1", "next", true));
    var response = new TaskCompletionSource<CommunityPendingReportsResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingReportRequests.Enqueue(_ =>
    {
      requestStarted.SetResult();
      return response.Task;
    });
    var continuation = viewModel.LoadMorePendingReportsAsync(TestContext.Current.CancellationToken);
    await requestStarted.Task;

    await viewModel.SelectSectionAsync(
        CommunityDetailSurfaceSection.Overview,
        TestContext.Current.CancellationToken);
    response.SetResult(PendingPage("stale-report", null, false));
    await continuation;

    Assert.Empty(viewModel.PendingReportRows);
    Assert.False(viewModel.IsLoadingMorePendingReports);
    Assert.False(viewModel.HasPendingReportPaginationError);
  }

  [Fact]
  public void SharedFixtureDeserializesEveryPendingReportField()
  {
    var response = SharedPendingReportsFixture();
    var report = Assert.Single(response.Reports);

    Assert.Equal(FullReportId, report.Id);
    Assert.Equal("00000000-0000-7000-8000-000000000602", report.CaseId);
    Assert.Equal("post", report.EntityType);
    Assert.Equal("00000000-0000-7000-8000-000000000603", report.EntityId);
    Assert.Equal("Fixture report target", report.TargetLabel);
    Assert.Equal("/discussion/fixture-report-target", report.TargetPath);
    Assert.Equal("spam", report.Reason);
    Assert.Equal("pending", report.Status);
    Assert.Equal(3, report.ReportCount);
    Assert.Equal(DateTimeOffset.Parse("2026-07-06T12:00:00Z"), report.CreatedAt);
    Assert.Null(report.ReviewedAt);
    Assert.Equal("00000000-0000-7000-8000-000000000604", report.TargetUserId);
    Assert.Equal("00000000-0000-7000-8000-000000000605", report.ReporterUserId);
    Assert.Equal("fixture-reporter", report.ReporterUsername);
    Assert.Equal("Fixture moderator note", report.Note);
    Assert.Null(report.ResolvedById);
    Assert.Equal("/admin/posts/00000000-0000-7000-8000-000000000603", report.AdminActionPath);
    Assert.True(report.TargetAvailable);
    Assert.False(report.TargetIsRestricted);
    Assert.False(report.IsSystemGenerated);
    Assert.Null(report.Judgement);
    Assert.Null(report.PostModerationContext);
    Assert.Null(report.CommunityBanEvasion);
    var claim = Assert.IsType<CommunityModerationQueueClaim>(report.Claim);
    Assert.Equal("00000000-0000-7000-8000-000000000608", claim.Id);
    Assert.Equal("00000000-0000-7000-8000-000000000609", claim.CommunityId);
    Assert.Equal(FullReportId, claim.ReportId);
    Assert.Equal("00000000-0000-7000-8000-000000000603", claim.PostId);
    Assert.Equal("00000000-0000-7000-8000-000000000610", claim.ClaimedById);
    Assert.Equal(DateTimeOffset.Parse("2026-07-06T12:30:00Z"), claim.ClaimedAt);
    Assert.Null(claim.ReleasedAt);
    Assert.Equal(DateTimeOffset.Parse("2026-07-06T12:45:00Z"), report.EscalatedAt);
    Assert.Equal("00000000-0000-7000-8000-000000000611", report.EscalatedById);
    Assert.False(response.PageInfo.HasNextPage);
  }

  [Fact]
  public void PendingReportConstructorDefaultsRemainStable()
  {
    var report = new CommunityPendingReport("report-defaults");

    Assert.Equal("report-defaults", report.Id);
    Assert.Null(report.CaseId);
    Assert.Null(report.EntityType);
    Assert.Null(report.EntityId);
    Assert.Null(report.TargetLabel);
    Assert.Null(report.TargetPath);
    Assert.Null(report.Reason);
    Assert.Equal("pending", report.Status);
    Assert.Equal(1, report.ReportCount);
    Assert.Equal(default, report.CreatedAt);
    Assert.Null(report.ReviewedAt);
    Assert.Null(report.TargetUserId);
    Assert.Null(report.ReporterUserId);
    Assert.Null(report.ReporterUsername);
    Assert.Null(report.Note);
    Assert.Null(report.ResolvedById);
    Assert.Null(report.AdminActionPath);
    Assert.Null(report.TargetAvailable);
    Assert.False(report.TargetIsRestricted);
    Assert.False(report.IsSystemGenerated);
    Assert.Null(report.Judgement);
    Assert.Null(report.PostModerationContext);
    Assert.Null(report.CommunityBanEvasion);
    Assert.Null(report.Claim);
    Assert.Null(report.EscalatedAt);
    Assert.Null(report.EscalatedById);
  }

  private static async Task<(CommunityDetailViewModel ViewModel, ScriptedCommunitiesService Service)>
      CreateViewModelAsync(CommunityPendingReportsResponse initialPage)
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    service.PendingReportRequests.Enqueue(_ => Task.FromResult(initialPage));
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadSurfaceAsync(
        "community-1",
        CommunityDetailSurfaceSection.Moderation,
        TestContext.Current.CancellationToken);
    return (viewModel, service);
  }

  private static CommunityPendingReportsResponse SharedPendingReportsFixture() =>
      JsonSerializer.Deserialize<CommunityPendingReportsResponse>(
          ApiFixtureLoader.LoadResponse("native.community.pending-reports.paginated"),
          VouchaApiJson.Options) ?? throw new InvalidOperationException("Pending report fixture did not deserialize.");

  private static CommunityPendingReportsResponse PendingPage(string id, string? cursor, bool hasNext) =>
      new([new CommunityPendingReport(id, EntityId: $"post-{id}")], new PageInfo(cursor, hasNext, null));

  private static CommunityModerationQueueResponse QueueResponse(string id) =>
      JsonSerializer.Deserialize<CommunityModerationQueueResponse>(
          $$$"""
          {"entries":[{"id":"{{{id}}}","community_id":"community-1","entity_type":"post",
          "entity_id":"post-1","post_id":"post-1","status":"pending","queue_source":"report",
          "reason":"spam","note":null,"report_count":1,"action_at":"2026-07-01T00:00:00Z",
          "created_at":"2026-07-01T00:00:00Z","target_label":"Post","target_path":null,
          "target_available":true,"target_is_anonymous":false,"target_is_restricted":false,
          "target_user_id":null,"reporter_user_id":null,"reporter_username":null,
          "resolved_by_id":null,"judgement":null,"flagged_reason":"spam","admin_action_path":null,
          "community_ban_evasion":null,"post_moderation_context":null,"is_system_generated":false,
          "cursor_created_at":null,"cursor_report_count":null,"cursor_severity_rank":null,
          "target_content":{"kind":"post","text":"محتوى مبلّغ عنه","declared_language":"ar",
          "lingua_rs_detected_language":null}}],
          "page_info":{"has_next_page":false},"viewer_tier":"moderator"}
          """,
          VouchaApiJson.Options)!;
}

internal sealed partial class ScriptedCommunitiesService
{
  public Queue<CommunityPendingReportsResponse> PendingReportResponses { get; } = [];
  public Queue<Func<CancellationToken, Task<CommunityPendingReportsResponse>>> PendingReportRequests { get; } = [];
  public List<string?> PendingReportCursors { get; } = [];

  public Task<CommunityPendingReportsResponse> FetchPendingReportsPageAsync(
      string idOrSlug,
      string? after,
      int limit,
      CancellationToken cancellationToken = default)
  {
    PendingReportCursors.Add(after);
    if (PendingReportRequests.TryDequeue(out var request)) return request(cancellationToken);
    return Task.FromResult(PendingReportResponses.Count == 0
        ? new CommunityPendingReportsResponse([], new PageInfo(null, false, null))
        : PendingReportResponses.Dequeue());
  }
}

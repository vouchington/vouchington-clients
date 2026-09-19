using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task ModerationClientMethodsUseExpectedRoutes()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.appeals.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.disputes.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.reports.clustered.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.reports.default")),
      new RecordedResponse("{}"),
      new RecordedResponse("{}"),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.clearance.approved")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.modlog.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.analytics.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.pending")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.resolution.dismissed")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.penalty")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.pending")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.resolution.dismissed")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.penalty")),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var token = TestContext.Current.CancellationToken;

    await client.FetchAppealsAsync(status: ModerationAppealStatus.Pending, limit: 11, after: "cursor-1", mine: true, cancellationToken: token);
    await client.FetchDisputesAsync(status: "pending", limit: 12, after: "cursor-2", mine: true, cancellationToken: token);
    await client.FetchClusteredModerationReportsAsync(after: "cursor-3", limit: 13, cancellationToken: token);
    await client.FetchStaffModerationReportsAsync(status: "pending", sort: "severity", after: "cursor-4", limit: 14, cancellationToken: token);
    await client.ResolveModerationReportAsync("report-1", ModerationReportResolution.Reviewed, token);
    await client.RerunModerationReportJudgementAsync("report-1", token);
    await client.FetchAdminReviewQueueAsync("cursor-5", 15, token);
    var clearance = await client.UpdatePostClearanceAsync("post-1", PostClearanceAction.Approved, token);
    await client.FetchAdminModlogAsync(new AdminModlogRequest("community-1", "user-1", "ban", "cursor-6"), token);
    await client.FetchAdminModerationAnalyticsAsync("90d", token);
    await client.FetchVoteIntegrityFlagsAsync(IntegrityFlagStatus.Pending, "cursor-7", 16, token);
    var resolvedVote = await client.ResolveVoteIntegrityFlagAsync(
        "flag-1", VoteIntegrityResolution.Dismissed, token);
    var votePenalty = await client.ApplyVoteIntegrityPenaltyAsync("flag-1", token);
    await client.FetchReportIntegrityFlagsAsync(
        IntegrityFlagStatus.Pending, "cursor-8", 17, token);
    var resolvedReport = await client.ResolveReportIntegrityFlagAsync(
        "flag-2", ReportIntegrityPatchResolution.Dismissed, token);
    var reportPenalty = await client.ApplyReportIntegrityPenaltyAsync("flag-2", token);

    Assert.Equal("/api/v1/appeals?after=cursor-1&limit=11&mine=true&status=pending", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/disputes?after=cursor-2&limit=12&mine=true&status=pending", handler.Requests[1].PathAndQuery);
    Assert.Equal("/api/v1/reports?after=cursor-3&cluster=entity&limit=13&status=pending", handler.Requests[2].PathAndQuery);
    Assert.Equal("/api/v1/reports?after=cursor-4&limit=14&sort=severity&status=pending", handler.Requests[3].PathAndQuery);
    Assert.Equal("/api/v1/reports/report-1", handler.Requests[4].PathAndQuery);
    Assert.Equal("""{"status":"reviewed"}""", handler.Requests[4].Body);
    Assert.Equal("/api/v1/reports/report-1/judgements", handler.Requests[5].PathAndQuery);
    Assert.Equal("/api/v1/posts/review-queue?after=cursor-5&limit=15", handler.Requests[6].PathAndQuery);
    Assert.Equal("/api/v1/posts/post-1/clearances", handler.Requests[7].PathAndQuery);
    Assert.Equal("""{"status":"approved","reason_code":"staff_approved"}""", handler.Requests[7].Body);
    Assert.Equal(AdminReviewQueueClearanceStatus.Approved, clearance.ClearanceStatus);
    Assert.Equal("/api/v1/admin/modlog?action_type=ban&actor_id=user-1&after=cursor-6&community_id=community-1", handler.Requests[8].PathAndQuery);
    Assert.Equal("/api/v1/admin/moderation-analytics?range=90d", handler.Requests[9].PathAndQuery);
    Assert.Equal("/api/v1/vote-integrity/flags?after=cursor-7&limit=16&status=pending", handler.Requests[10].PathAndQuery);
    Assert.Equal("/api/v1/vote-integrity/flags/flag-1", handler.Requests[11].PathAndQuery);
    Assert.Equal("""{"resolution":"dismissed"}""", handler.Requests[11].Body);
    Assert.Equal("/api/v1/vote-integrity/flags/flag-1/penalties", handler.Requests[12].PathAndQuery);
    Assert.Equal("/api/v1/report-integrity/flags?after=cursor-8&limit=17&status=pending", handler.Requests[13].PathAndQuery);
    Assert.Equal("/api/v1/report-integrity/flags/flag-2", handler.Requests[14].PathAndQuery);
    Assert.Equal("""{"resolution":"dismissed"}""", handler.Requests[14].Body);
    Assert.Equal("/api/v1/report-integrity/flags/flag-2/penalties", handler.Requests[15].PathAndQuery);
    Assert.Equal("dismissed", resolvedVote.Flag.Resolution);
    Assert.Equal(2, votePenalty.PenalizedUserCount);
    Assert.Equal("dismissed", resolvedReport.Flag.Resolution);
    Assert.Equal("penalized", reportPenalty.Flag.Resolution);
    Assert.Equal(2, reportPenalty.PenalizedUserCount);
    Assert.Equal(2, reportPenalty.Penalties.Count);
  }
}

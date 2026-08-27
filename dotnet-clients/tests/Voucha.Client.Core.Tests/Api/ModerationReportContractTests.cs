using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationReportContractTests
{
  [Fact]
  public void SharedReportFixturesDecodeThroughAudienceSpecificTypes()
  {
    var staff = Decode<StaffFlatModerationReportsResponse>("native.moderation.reports.default");
    var member = Decode<MemberFlatModerationReportsResponse>("native.moderation.reports.member.default");
    var clustered = Decode<StaffClusteredModerationReportsResponse>("native.moderation.reports.clustered.default");
    var resolution = Decode<ModerationReportResolutionResponse>("native.moderation.report-resolution.reviewed");
    var judgement = Decode<ModerationReportJudgementResponse>("native.moderation.report-judgement.default");
    var warning = Decode<AdminUserWarningResponse>("native.moderation.admin-warning.report");

    Assert.Equal("alice", staff.Reports.Single().ReporterUsername);
    Assert.Equal("case-1", member.Reports.Single().CaseId);
    Assert.Contains(
        clustered.Clusters,
        cluster => cluster.EntityId == "00000000-0000-7000-8000-000000000101");
    var userCluster = Assert.Single(clustered.Clusters, cluster => cluster.EntityType == "user");
    Assert.Contains(userCluster.Reports, report => !report.IsSystemGenerated);
    Assert.Contains(userCluster.Reports, report => report.CommunityBanEvasion is not null);
    var duplicate = Assert.Single(clustered.DuplicateClusters);
    Assert.Equal(3, duplicate.PostCount);
    Assert.Equal(3, duplicate.ReportCount);
    Assert.Equal(3, duplicate.Clusters.SelectMany(cluster => cluster.Reports).Count());
    Assert.Equal(ModerationReportResolution.Reviewed, resolution.Report.Status);
    Assert.True(judgement.Queued);
    Assert.Equal("user-2", warning.Warning.UserId);
    Assert.Null(warning.Warning.CommunityId);
  }

  [Fact]
  public async Task ReportMutationEndpointsUseTypedBodiesAndResponses()
  {
    var handler = new RecordingHandler([
      Fixture("native.moderation.report-resolution.reviewed"),
      Fixture("native.moderation.report-judgement.default", HttpStatusCode.Accepted),
      Fixture("native.moderation.admin-warning.report", HttpStatusCode.Created),
      new RecordedResponse("null", HttpStatusCode.NoContent),
      new RecordedResponse("null", HttpStatusCode.NoContent),
    ]);
    var client = Client(handler);
    var token = TestContext.Current.CancellationToken;

    var resolution = await client.ResolveModerationReportAsync(
        "report-1", ModerationReportResolution.Reviewed, token);
    var judgement = await client.RerunModerationReportJudgementAsync("report-1", token);
    var warning = await client.IssueAdminWarningAsync(
        new IssueAdminWarningRequest(
            "user-2", "Repeated harassment", "Stop contacting this user.", "report-1"), token);
    await client.ConfirmReportBanEvasionAsync("community-1", "user-2", token);
    await client.DismissReportBanEvasionAsync("community-1", "user-2", token);

    Assert.Equal(ModerationReportResolution.Reviewed, resolution.Report.Status);
    Assert.True(judgement.Queued);
    Assert.Equal("warning-1", warning.Warning.Id);
    Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
    Assert.Equal("""{"status":"reviewed"}""", handler.Requests[0].Body);
    Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
    Assert.Equal("/api/v1/admin/warnings", handler.Requests[2].PathAndQuery);
    Assert.Equal(
        """{"userId":"user-2","reason":"Repeated harassment","publicMessage":"Stop contacting this user.","reportId":"report-1","resolveReport":true}""",
        handler.Requests[2].Body);
    Assert.Equal(HttpMethod.Post, handler.Requests[3].Method);
    Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
  }

  [Fact]
  public void AdminWarningValidationEnforcesApiLimits()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => VouchaApiEndpoints.IssueAdminWarning(
        new IssueAdminWarningRequest("user-2", new string('r', 1_001), null, "report-1")));
    Assert.Throws<ArgumentOutOfRangeException>(() => VouchaApiEndpoints.IssueAdminWarning(
        new IssueAdminWarningRequest("user-2", "reason", new string('p', 2_001), "report-1")));
    var request = VouchaApiEndpoints.IssueAdminWarning(
        new IssueAdminWarningRequest("user-2", new string('r', 1_000), new string('p', 2_000), "report-1"));
    Assert.Equal("/api/v1/admin/warnings", request.Path);
  }

  private static T Decode<T>(string fixtureId) where T : class =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)!;

  private static RecordedResponse Fixture(string fixtureId, HttpStatusCode status = HttpStatusCode.OK) =>
      new(ApiFixtureLoader.LoadResponse(fixtureId), status);

  private static VouchaApiClient Client(RecordingHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
}

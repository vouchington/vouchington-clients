using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  [Theory]
  [MemberData(nameof(ModerationOpsEndpointCases))]
  public void ModerationOpsEndpointsMirrorSwiftRoutes(
      string name,
      ApiRequest request,
      HttpMethod method,
      string path,
      IReadOnlyDictionary<string, string> query,
      bool hasBody)
  {
    AssertEndpoint(name, request, method, path, query, hasBody);
  }

  public static IEnumerable<object[]> ModerationOpsEndpointCases()
  {
    yield return Case("moderationReports", VouchaApiEndpoints.ModerationReports("pending", "severity", "cursor-1", 25), HttpMethod.Get, "/api/v1/reports", Query(("limit", "25"), ("status", "pending"), ("sort", "severity"), ("after", "cursor-1")));
    yield return Case("clusteredModerationReports", VouchaApiEndpoints.ClusteredModerationReports("pending", "cursor-2"), HttpMethod.Get, "/api/v1/reports", Query(("limit", "25"), ("cluster", "entity"), ("status", "pending"), ("after", "cursor-2")));
    yield return Case("resolveModerationReport", VouchaApiEndpoints.ResolveModerationReport("report-1", ModerationReportResolution.Reviewed), HttpMethod.Patch, "/api/v1/reports/report-1", Query(), true);
    yield return Case("rerunModerationReportJudgement", VouchaApiEndpoints.RerunModerationReportJudgement("report-1"), HttpMethod.Post, "/api/v1/reports/report-1/judgements", Query(), false);
    yield return Case("adminReviewQueue", VouchaApiEndpoints.AdminReviewQueue("cursor-3", 13), HttpMethod.Get, "/api/v1/posts/review-queue", Query(("limit", "13"), ("after", "cursor-3")));
    yield return Case("updatePostClearance", VouchaApiEndpoints.UpdatePostClearance("post-1", PostClearanceAction.Approved), HttpMethod.Post, "/api/v1/posts/post-1/clearances", Query(), true);
    yield return Case("adminModlog", VouchaApiEndpoints.AdminModlog(communityId: "community-1", actorId: "user-1", actionType: "ban", after: "cursor-4"), HttpMethod.Get, "/api/v1/admin/modlog", Query(("community_id", "community-1"), ("actor_id", "user-1"), ("action_type", "ban"), ("after", "cursor-4")));
    yield return Case("adminModerationAnalytics", VouchaApiEndpoints.AdminModerationAnalytics("90d"), HttpMethod.Get, "/api/v1/admin/moderation-analytics", Query(("range", "90d")));
    yield return Case("moderationTransparency", VouchaApiEndpoints.ModerationTransparency("90d"), HttpMethod.Get, "/api/v1/moderation-transparency", Query(("range", "90d")));
    yield return Case("voteIntegrityFlags", VouchaApiEndpoints.VoteIntegrityFlags(IntegrityFlagStatus.Pending, "cursor-5"), HttpMethod.Get, "/api/v1/vote-integrity/flags", Query(("status", "pending"), ("after", "cursor-5")));
    yield return Case("voteIntegrityFlag", VouchaApiEndpoints.VoteIntegrityFlag("flag-1"), HttpMethod.Get, "/api/v1/vote-integrity/flags/flag-1", Query());
    yield return Case("resolveVoteIntegrityFlag", VouchaApiEndpoints.ResolveVoteIntegrityFlag("flag-1", VoteIntegrityResolution.Dismissed), HttpMethod.Patch, "/api/v1/vote-integrity/flags/flag-1", Query(), true);
    yield return Case("applyVoteIntegrityPenalty", VouchaApiEndpoints.ApplyVoteIntegrityPenalty("flag-1"), HttpMethod.Post, "/api/v1/vote-integrity/flags/flag-1/penalties", Query(), false);
    yield return Case("voteIntegrityPenalties", VouchaApiEndpoints.VoteIntegrityPenalties(IntegrityPenaltyStatus.Active, "cursor-v", 20, "user-1", "flag-1"), HttpMethod.Get, "/api/v1/vote-integrity/penalties", Query(("status", "active"), ("source", "flag"), ("user_id", "user-1"), ("source_flag_id", "flag-1"), ("after", "cursor-v"), ("limit", "20")));
    yield return Case("voteIntegrityPenalty", VouchaApiEndpoints.VoteIntegrityPenalty("penalty-1"), HttpMethod.Get, "/api/v1/vote-integrity/penalties/penalty-1", Query());
    yield return Case("revokeVoteIntegrityPenalty", VouchaApiEndpoints.RevokeVoteIntegrityPenalty("penalty-1"), HttpMethod.Delete, "/api/v1/vote-integrity/penalties/penalty-1", Query());
    yield return Case("reportIntegrityFlags", VouchaApiEndpoints.ReportIntegrityFlags(IntegrityFlagStatus.Pending, "cursor-6"), HttpMethod.Get, "/api/v1/report-integrity/flags", Query(("status", "pending"), ("after", "cursor-6")));
    yield return Case("reportIntegrityFlag", VouchaApiEndpoints.ReportIntegrityFlag("flag-2"), HttpMethod.Get, "/api/v1/report-integrity/flags/flag-2", Query());
    yield return Case("resolveReportIntegrityFlag", VouchaApiEndpoints.ResolveReportIntegrityFlag("flag-2", ReportIntegrityPatchResolution.Dismissed), HttpMethod.Patch, "/api/v1/report-integrity/flags/flag-2", Query(), true);
    yield return Case("applyReportIntegrityPenalty", VouchaApiEndpoints.ApplyReportIntegrityPenalty("flag-2"), HttpMethod.Post, "/api/v1/report-integrity/flags/flag-2/penalties", Query(), false);
    yield return Case("reportIntegrityPenalties", VouchaApiEndpoints.ReportIntegrityPenalties(IntegrityPenaltyStatus.Revoked, "cursor-r", 21, "user-2", "flag-2"), HttpMethod.Get, "/api/v1/report-integrity/penalties", Query(("status", "revoked"), ("user_id", "user-2"), ("source_flag_id", "flag-2"), ("after", "cursor-r"), ("limit", "21")));
    yield return Case("reportIntegrityPenalty", VouchaApiEndpoints.ReportIntegrityPenalty("penalty-2"), HttpMethod.Get, "/api/v1/report-integrity/penalties/penalty-2", Query());
    yield return Case("revokeReportIntegrityPenalty", VouchaApiEndpoints.RevokeReportIntegrityPenalty("penalty-2"), HttpMethod.Delete, "/api/v1/report-integrity/penalties/penalty-2", Query());
  }

  [Fact]
  public void AllFiltersOmitStatusOnTheWire()
  {
    Assert.DoesNotContain("status", VouchaApiEndpoints.ReportIntegrityFlags(IntegrityFlagStatus.All).Query.Keys);
    Assert.DoesNotContain("status", VouchaApiEndpoints.VoteIntegrityFlags(IntegrityFlagStatus.All).Query.Keys);
    Assert.DoesNotContain("status", VouchaApiEndpoints.ReportIntegrityPenalties(IntegrityPenaltyStatus.All).Query.Keys);
    Assert.DoesNotContain("status", VouchaApiEndpoints.VoteIntegrityPenalties(IntegrityPenaltyStatus.All).Query.Keys);
  }
}

namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest ModerationReports(string? status = "pending", string? sort = null, string? after = null, int limit = 25) =>
      Get("/api/v1/reports", Query(("limit", limit), ("status", status), ("sort", sort), ("after", after)));

  public static ApiRequest ClusteredModerationReports(string? status = "pending", string? after = null, int limit = 25) =>
      Get("/api/v1/reports", Query(("limit", limit), ("cluster", "entity"), ("status", status), ("after", after)));

  public static ApiRequest ResolveModerationReport(string reportId, ModerationReportResolution status) =>
      new(HttpMethod.Patch, $"/api/v1/reports/{Path(reportId)}") { Body = new ResolveModerationReportBody(status) };

  public static ApiRequest RerunModerationReportJudgement(string reportId) =>
      new(HttpMethod.Post, $"/api/v1/reports/{Path(reportId)}/judgements");

  public static ApiRequest IssueAdminWarning(IssueAdminWarningRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);
    request.Validate();
    return new(HttpMethod.Post, "/api/v1/admin/warnings") { Body = request };
  }

  public static ApiRequest ConfirmReportBanEvasion(string communityIdOrSlug, string userId) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(communityIdOrSlug)}/ban-evasion/{Path(userId)}");

  public static ApiRequest DismissReportBanEvasion(string communityIdOrSlug, string userId) =>
      new(HttpMethod.Delete, $"/api/v1/communities/{Path(communityIdOrSlug)}/ban-evasion/{Path(userId)}");

  public static ApiRequest AdminReviewQueue(string? after = null, int limit = 25) =>
      Get("/api/v1/posts/review-queue", Query(("limit", limit), ("after", after)));

  public static ApiRequest UpdatePostClearance(string postId, PostClearanceAction status) =>
      new(HttpMethod.Post, $"/api/v1/posts/{Path(postId)}/clearances") { Body = new UpdatePostClearanceBody(status) };

  public static ApiRequest AdminModlog(string? communityId = null, string? actorId = null, string? actionType = null, string? after = null) =>
      Get("/api/v1/admin/modlog", Query(("community_id", communityId), ("actor_id", actorId), ("action_type", actionType), ("after", after)));

  public static ApiRequest AdminModerationAnalytics(string range = "30d") =>
      Get("/api/v1/admin/moderation-analytics", Query(("range", range)));

  public static ApiRequest ModerationTransparency(string range = "30d", string? after = null) =>
      Get("/api/v1/moderation-transparency", Query(("range", range), ("after", after)));

  public static ApiRequest VoteIntegrityFlags(IntegrityFlagStatus? status = null, string? after = null, int? limit = null) =>
      Get("/api/v1/vote-integrity/flags", Query(("status", IntegrityStatus(status)), ("after", after), ("limit", limit)));

  public static ApiRequest VoteIntegrityFlag(string flagId) =>
      Get($"/api/v1/vote-integrity/flags/{Path(flagId)}");

  public static ApiRequest ResolveVoteIntegrityFlag(string flagId, VoteIntegrityResolution resolution) =>
      new(HttpMethod.Patch, $"/api/v1/vote-integrity/flags/{Path(flagId)}") { Body = new ResolveVoteIntegrityFlagBody(resolution) };

  public static ApiRequest ApplyVoteIntegrityPenalty(string flagId) =>
      new(HttpMethod.Post, $"/api/v1/vote-integrity/flags/{Path(flagId)}/penalties");

  public static ApiRequest VoteIntegrityPenalties(
      IntegrityPenaltyStatus? status = null,
      string? after = null,
      int? limit = null,
      string? userId = null,
      string? sourceFlagId = null) =>
      Get("/api/v1/vote-integrity/penalties", Query(
          ("status", PenaltyStatus(status)), ("source", "flag"),
          ("user_id", userId), ("source_flag_id", sourceFlagId),
          ("after", after), ("limit", limit)));

  public static ApiRequest VoteIntegrityPenalty(string penaltyId) =>
      Get($"/api/v1/vote-integrity/penalties/{Path(penaltyId)}");

  public static ApiRequest RevokeVoteIntegrityPenalty(string penaltyId) =>
      new(HttpMethod.Delete, $"/api/v1/vote-integrity/penalties/{Path(penaltyId)}");

  public static ApiRequest ReportIntegrityFlags(IntegrityFlagStatus? status = null, string? after = null, int? limit = null) =>
      Get("/api/v1/report-integrity/flags", Query(("status", IntegrityStatus(status)), ("after", after), ("limit", limit)));

  public static ApiRequest ReportIntegrityFlag(string flagId) =>
      Get($"/api/v1/report-integrity/flags/{Path(flagId)}");

  public static ApiRequest ResolveReportIntegrityFlag(string flagId, ReportIntegrityPatchResolution resolution) =>
      new(HttpMethod.Patch, $"/api/v1/report-integrity/flags/{Path(flagId)}") { Body = new ResolveReportIntegrityFlagBody(resolution) };

  public static ApiRequest ApplyReportIntegrityPenalty(string flagId) =>
      new(HttpMethod.Post, $"/api/v1/report-integrity/flags/{Path(flagId)}/penalties");

  public static ApiRequest ReportIntegrityPenalties(
      IntegrityPenaltyStatus? status = null,
      string? after = null,
      int? limit = null,
      string? userId = null,
      string? sourceFlagId = null) =>
      Get("/api/v1/report-integrity/penalties", Query(
          ("status", PenaltyStatus(status)), ("user_id", userId),
          ("source_flag_id", sourceFlagId), ("after", after), ("limit", limit)));

  public static ApiRequest ReportIntegrityPenalty(string penaltyId) =>
      Get($"/api/v1/report-integrity/penalties/{Path(penaltyId)}");

  public static ApiRequest RevokeReportIntegrityPenalty(string penaltyId) =>
      new(HttpMethod.Delete, $"/api/v1/report-integrity/penalties/{Path(penaltyId)}");

  private static string? IntegrityStatus(IntegrityFlagStatus? status) =>
      status switch
      {
        IntegrityFlagStatus.Pending => "pending",
        IntegrityFlagStatus.Resolved => "resolved",
        IntegrityFlagStatus.All => null,
        _ => null,
      };

  private static string? PenaltyStatus(IntegrityPenaltyStatus? status) => status switch
  {
    IntegrityPenaltyStatus.Active => "active",
    IntegrityPenaltyStatus.Revoked => "revoked",
    _ => null,
  };
}

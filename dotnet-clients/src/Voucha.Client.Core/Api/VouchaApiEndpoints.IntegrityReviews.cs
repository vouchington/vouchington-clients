namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest VoteIntegrityPenaltyReviewsDue(string? after = null, int? limit = null) =>
      Get("/api/v1/vote-integrity/penalties/review-due", Query(("after", after), ("limit", limit)));

  public static ApiRequest ReviewVoteIntegrityPenalty(string penaltyId) =>
      new(HttpMethod.Post, $"/api/v1/vote-integrity/penalties/{Path(penaltyId)}/reviews");

  public static ApiRequest ReportIntegrityPenaltyReviewsDue(string? after = null, int? limit = null) =>
      Get("/api/v1/report-integrity/penalties/review-due", Query(("after", after), ("limit", limit)));

  public static ApiRequest ReviewReportIntegrityPenalty(string penaltyId) =>
      new(HttpMethod.Post, $"/api/v1/report-integrity/penalties/{Path(penaltyId)}/reviews");
}

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<VoteIntegrityPenaltiesResponse> FetchVoteIntegrityPenaltyReviewsDueAsync(
      string? after = null, int? limit = null, CancellationToken cancellationToken = default) =>
      SendAsync<VoteIntegrityPenaltiesResponse>(VouchaApiEndpoints.VoteIntegrityPenaltyReviewsDue(after, limit), cancellationToken);

  public Task<VoteWeightPenaltyResponse> ReviewVoteIntegrityPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      SendAsync<VoteWeightPenaltyResponse>(VouchaApiEndpoints.ReviewVoteIntegrityPenalty(penaltyId), cancellationToken);

  public Task<ReportIntegrityPenaltiesResponse> FetchReportIntegrityPenaltyReviewsDueAsync(
      string? after = null, int? limit = null, CancellationToken cancellationToken = default) =>
      SendAsync<ReportIntegrityPenaltiesResponse>(VouchaApiEndpoints.ReportIntegrityPenaltyReviewsDue(after, limit), cancellationToken);

  public Task<ReportAbusePenaltyResponse> ReviewReportIntegrityPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      SendAsync<ReportAbusePenaltyResponse>(VouchaApiEndpoints.ReviewReportIntegrityPenalty(penaltyId), cancellationToken);
}

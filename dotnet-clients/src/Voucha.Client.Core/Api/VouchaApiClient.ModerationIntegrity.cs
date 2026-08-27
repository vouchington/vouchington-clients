namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<VoteIntegrityFlagsResponse> FetchVoteIntegrityFlagsAsync(
      IntegrityFlagStatus? status = null, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<VoteIntegrityFlagsResponse>(
          VouchaApiEndpoints.VoteIntegrityFlags(status, after, limit), cancellationToken);

  public Task<VoteIntegrityFlagResponse> ResolveVoteIntegrityFlagAsync(
      string flagId, VoteIntegrityResolution resolution,
      CancellationToken cancellationToken = default) =>
      SendAsync<VoteIntegrityFlagResponse>(
          VouchaApiEndpoints.ResolveVoteIntegrityFlag(flagId, resolution), cancellationToken);

  public Task<VoteIntegrityFlagResponse> FetchVoteIntegrityFlagAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      SendAsync<VoteIntegrityFlagResponse>(
          VouchaApiEndpoints.VoteIntegrityFlag(flagId), cancellationToken);

  public Task<VoteIntegrityPenaltyApplicationResponse> ApplyVoteIntegrityPenaltyAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      SendAsync<VoteIntegrityPenaltyApplicationResponse>(
          VouchaApiEndpoints.ApplyVoteIntegrityPenalty(flagId), cancellationToken);

  public Task<VoteIntegrityPenaltiesResponse> FetchVoteIntegrityPenaltiesAsync(
      IntegrityPenaltyStatus status = IntegrityPenaltyStatus.Active,
      string? after = null, int limit = 25, string? userId = null,
      string? sourceFlagId = null, CancellationToken cancellationToken = default) =>
      SendAsync<VoteIntegrityPenaltiesResponse>(
          VouchaApiEndpoints.VoteIntegrityPenalties(status, after, limit, userId, sourceFlagId),
          cancellationToken);

  public Task<VoteWeightPenaltyResponse> FetchVoteIntegrityPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      SendAsync<VoteWeightPenaltyResponse>(
          VouchaApiEndpoints.VoteIntegrityPenalty(penaltyId), cancellationToken);

  public Task<VoteWeightPenaltyResponse> RevokeVoteIntegrityPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      SendAsync<VoteWeightPenaltyResponse>(
          VouchaApiEndpoints.RevokeVoteIntegrityPenalty(penaltyId), cancellationToken);

  public Task<ReportIntegrityFlagsResponse> FetchReportIntegrityFlagsAsync(
      IntegrityFlagStatus? status = null, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<ReportIntegrityFlagsResponse>(
          VouchaApiEndpoints.ReportIntegrityFlags(status, after, limit), cancellationToken);

  public Task<ReportIntegrityFlagResponse> ResolveReportIntegrityFlagAsync(
      string flagId, ReportIntegrityPatchResolution resolution,
      CancellationToken cancellationToken = default) =>
      SendAsync<ReportIntegrityFlagResponse>(
          VouchaApiEndpoints.ResolveReportIntegrityFlag(flagId, resolution), cancellationToken);

  public Task<ReportIntegrityFlagResponse> FetchReportIntegrityFlagAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      SendAsync<ReportIntegrityFlagResponse>(
          VouchaApiEndpoints.ReportIntegrityFlag(flagId), cancellationToken);

  public Task<ReportIntegrityPenaltyResponse> ApplyReportIntegrityPenaltyAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      SendAsync<ReportIntegrityPenaltyResponse>(
          VouchaApiEndpoints.ApplyReportIntegrityPenalty(flagId), cancellationToken);

  public Task<ReportIntegrityPenaltiesResponse> FetchReportIntegrityPenaltiesAsync(
      IntegrityPenaltyStatus status = IntegrityPenaltyStatus.Active,
      string? after = null, int limit = 25, string? userId = null,
      string? sourceFlagId = null, CancellationToken cancellationToken = default) =>
      SendAsync<ReportIntegrityPenaltiesResponse>(
          VouchaApiEndpoints.ReportIntegrityPenalties(status, after, limit, userId, sourceFlagId),
          cancellationToken);

  public Task<ReportAbusePenaltyResponse> FetchReportIntegrityPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      SendAsync<ReportAbusePenaltyResponse>(
          VouchaApiEndpoints.ReportIntegrityPenalty(penaltyId), cancellationToken);

  public Task<ReportAbusePenaltyResponse> RevokeReportIntegrityPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      SendAsync<ReportAbusePenaltyResponse>(
          VouchaApiEndpoints.RevokeReportIntegrityPenalty(penaltyId), cancellationToken);
}

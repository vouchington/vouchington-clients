using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ModerationIntegrity;

public interface IModerationIntegrityService
{
  Task<ReportIntegrityFlagsResponse> FetchReportFlagsAsync(
      IntegrityFlagStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<ReportIntegrityFlagResponse> DismissReportFlagAsync(
      string flagId,
      CancellationToken cancellationToken = default);

  Task<ReportIntegrityFlagResponse> FetchReportFlagAsync(
      string flagId, CancellationToken cancellationToken = default);

  Task<ReportIntegrityPenaltyResponse> PenalizeReportersAsync(
      string flagId,
      CancellationToken cancellationToken = default);

  Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltiesAsync(
      IntegrityPenaltyStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<ReportAbusePenaltyResponse> FetchReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default);

  Task<ReportAbusePenaltyResponse> RevokeReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default);

  Task<VoteIntegrityFlagsResponse> FetchVoteFlagsAsync(
      IntegrityFlagStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default);

  Task<VoteIntegrityFlagResponse> ResolveVoteFlagAsync(
      string flagId,
      VoteIntegrityResolution resolution,
      CancellationToken cancellationToken = default);

  Task<VoteIntegrityFlagResponse> FetchVoteFlagAsync(
      string flagId, CancellationToken cancellationToken = default);

  Task<VoteIntegrityPenaltyApplicationResponse> ApplyVoteRingPenaltyAsync(
      string flagId,
      CancellationToken cancellationToken = default);

  Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltiesAsync(
      IntegrityPenaltyStatus status,
      string? after = null,
      int limit = 25,
      string? sourceFlagId = null,
      CancellationToken cancellationToken = default);

  Task<VoteWeightPenaltyResponse> FetchVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default);

  Task<VoteWeightPenaltyResponse> RevokeVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default);
}

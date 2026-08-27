using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed class ApiModerationIntegrityService(VouchaApiClient client) : IModerationIntegrityService
{
  public Task<ReportIntegrityFlagsResponse> FetchReportFlagsAsync(
      IntegrityFlagStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchReportIntegrityFlagsAsync(status, after, limit, cancellationToken);

  public Task<ReportIntegrityFlagResponse> DismissReportFlagAsync(
      string flagId,
      CancellationToken cancellationToken = default) =>
      client.ResolveReportIntegrityFlagAsync(
          flagId,
          ReportIntegrityPatchResolution.Dismissed,
          cancellationToken);

  public Task<ReportIntegrityFlagResponse> FetchReportFlagAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      client.FetchReportIntegrityFlagAsync(flagId, cancellationToken);

  public Task<ReportIntegrityPenaltyResponse> PenalizeReportersAsync(
      string flagId,
      CancellationToken cancellationToken = default) =>
      client.ApplyReportIntegrityPenaltyAsync(flagId, cancellationToken);

  public Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltiesAsync(
      IntegrityPenaltyStatus status, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchReportIntegrityPenaltiesAsync(
          status, after, limit, cancellationToken: cancellationToken);

  public Task<ReportAbusePenaltyResponse> FetchReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      client.FetchReportIntegrityPenaltyAsync(penaltyId, cancellationToken);

  public Task<ReportAbusePenaltyResponse> RevokeReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      client.RevokeReportIntegrityPenaltyAsync(penaltyId, cancellationToken);

  public Task<VoteIntegrityFlagsResponse> FetchVoteFlagsAsync(
      IntegrityFlagStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      client.FetchVoteIntegrityFlagsAsync(status, after, limit, cancellationToken);

  public Task<VoteIntegrityFlagResponse> ResolveVoteFlagAsync(
      string flagId,
      VoteIntegrityResolution resolution,
      CancellationToken cancellationToken = default) =>
      client.ResolveVoteIntegrityFlagAsync(flagId, resolution, cancellationToken);

  public Task<VoteIntegrityFlagResponse> FetchVoteFlagAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      client.FetchVoteIntegrityFlagAsync(flagId, cancellationToken);

  public Task<VoteIntegrityPenaltyApplicationResponse> ApplyVoteRingPenaltyAsync(
      string flagId,
      CancellationToken cancellationToken = default) =>
      client.ApplyVoteIntegrityPenaltyAsync(flagId, cancellationToken);

  public async Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltiesAsync(
      IntegrityPenaltyStatus status, string? after = null, int limit = 25,
      string? sourceFlagId = null,
      CancellationToken cancellationToken = default)
  {
    var response = await client.FetchVoteIntegrityPenaltiesAsync(
        status, after, limit, sourceFlagId: sourceFlagId,
        cancellationToken: cancellationToken).ConfigureAwait(false);
    if (response.FilterScope is not { Source: "flag" } scope ||
        !string.Equals(scope.SourceFlagId, sourceFlagId, StringComparison.Ordinal))
      throw new InvalidOperationException("Vote penalty scope was not confirmed by the server.");
    return response;
  }

  public Task<VoteWeightPenaltyResponse> FetchVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      client.FetchVoteIntegrityPenaltyAsync(penaltyId, cancellationToken);

  public Task<VoteWeightPenaltyResponse> RevokeVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      client.RevokeVoteIntegrityPenaltyAsync(penaltyId, cancellationToken);
}

using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

internal sealed class ModerationIntegrityTestService : IModerationIntegrityService
{
  public Func<IntegrityFlagStatus, string?, CancellationToken, Task<ReportIntegrityFlagsResponse>>
      FetchReports
  { get; set; } = (_, _, _) => throw new InvalidOperationException("Unexpected report fetch.");
  public Func<string, CancellationToken, Task<ReportIntegrityFlagResponse>> DismissReport { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected report dismissal.");
  public Func<string, CancellationToken, Task<ReportIntegrityFlagResponse>> FetchReport { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected report reload.");
  public Func<string, CancellationToken, Task<ReportIntegrityPenaltyResponse>> PenalizeReporters { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected report penalty.");
  public Func<IntegrityFlagStatus, string?, CancellationToken, Task<VoteIntegrityFlagsResponse>>
      FetchVotes
  { get; set; } = (_, _, _) => throw new InvalidOperationException("Unexpected vote fetch.");
  public Func<string, VoteIntegrityResolution, CancellationToken, Task<VoteIntegrityFlagResponse>>
      ResolveVote
  { get; set; } = (_, _, _) => throw new InvalidOperationException("Unexpected vote resolution.");
  public Func<string, CancellationToken, Task<VoteIntegrityFlagResponse>> FetchVote { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected vote reload.");
  public Func<string, CancellationToken, Task<VoteIntegrityPenaltyApplicationResponse>> PenalizeVotes { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected vote penalty.");
  public Func<IntegrityPenaltyStatus, string?, CancellationToken, Task<ReportIntegrityPenaltiesResponse>> FetchReportPenalties { get; set; } =
      (_, _, _) => throw new InvalidOperationException("Unexpected report penalty fetch.");
  public Func<string, CancellationToken, Task<ReportAbusePenaltyResponse>> FetchReportPenalty { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected report penalty reload.");
  public Func<string, CancellationToken, Task<ReportAbusePenaltyResponse>> RevokeReportPenalty { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected report penalty revocation.");
  public Func<IntegrityPenaltyStatus, string?, string?, CancellationToken, Task<VoteIntegrityPenaltiesResponse>> FetchVotePenalties { get; set; } =
      (_, _, _, _) => throw new InvalidOperationException("Unexpected vote penalty fetch.");
  public Func<string, CancellationToken, Task<VoteWeightPenaltyResponse>> FetchVotePenalty { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected vote penalty reload.");
  public Func<string, CancellationToken, Task<VoteWeightPenaltyResponse>> RevokeVotePenalty { get; set; } =
      (_, _) => throw new InvalidOperationException("Unexpected vote penalty revocation.");

  public List<(IntegrityFlagStatus Status, string? After)> ReportFetches { get; } = [];
  public List<(IntegrityFlagStatus Status, string? After)> VoteFetches { get; } = [];
  public int ReportDismissals { get; private set; }
  public int ReportPenalties { get; private set; }
  public List<VoteIntegrityResolution> VoteResolutions { get; } = [];
  public int VotePenalties { get; private set; }

  public Task<ReportIntegrityFlagsResponse> FetchReportFlagsAsync(
      IntegrityFlagStatus status, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default)
  {
    ReportFetches.Add((status, after));
    return FetchReports(status, after, cancellationToken);
  }

  public Task<ReportIntegrityFlagResponse> DismissReportFlagAsync(
      string flagId, CancellationToken cancellationToken = default)
  {
    ReportDismissals++;
    return DismissReport(flagId, cancellationToken);
  }

  public Task<ReportIntegrityFlagResponse> FetchReportFlagAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      FetchReport(flagId, cancellationToken);

  public Task<ReportIntegrityPenaltyResponse> PenalizeReportersAsync(
      string flagId, CancellationToken cancellationToken = default)
  {
    ReportPenalties++;
    return PenalizeReporters(flagId, cancellationToken);
  }

  public Task<VoteIntegrityFlagsResponse> FetchVoteFlagsAsync(
      IntegrityFlagStatus status, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default)
  {
    VoteFetches.Add((status, after));
    return FetchVotes(status, after, cancellationToken);
  }

  public Task<VoteIntegrityFlagResponse> ResolveVoteFlagAsync(
      string flagId, VoteIntegrityResolution resolution,
      CancellationToken cancellationToken = default)
  {
    VoteResolutions.Add(resolution);
    return ResolveVote(flagId, resolution, cancellationToken);
  }

  public Task<VoteIntegrityFlagResponse> FetchVoteFlagAsync(
      string flagId, CancellationToken cancellationToken = default) =>
      FetchVote(flagId, cancellationToken);

  public Task<VoteIntegrityPenaltyApplicationResponse> ApplyVoteRingPenaltyAsync(
      string flagId, CancellationToken cancellationToken = default)
  {
    VotePenalties++;
    return PenalizeVotes(flagId, cancellationToken);
  }

  public Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltiesAsync(
      IntegrityPenaltyStatus status, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default) =>
      FetchReportPenalties(status, after, cancellationToken);

  public Task<ReportAbusePenaltyResponse> FetchReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      FetchReportPenalty(penaltyId, cancellationToken);

  public Task<ReportAbusePenaltyResponse> RevokeReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      RevokeReportPenalty(penaltyId, cancellationToken);

  public Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltiesAsync(
      IntegrityPenaltyStatus status, string? after = null, int limit = 25,
      string? sourceFlagId = null,
      CancellationToken cancellationToken = default) =>
      FetchVotePenalties(status, after, sourceFlagId, cancellationToken);

  public Task<VoteWeightPenaltyResponse> FetchVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      FetchVotePenalty(penaltyId, cancellationToken);

  public Task<VoteWeightPenaltyResponse> RevokeVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default) =>
      RevokeVotePenalty(penaltyId, cancellationToken);

  public static ReportIntegrityFlag ReportFlag(
      string id,
      string? resolution = null,
      string? postId = "post-1",
      string? userId = null,
      string? hostnameId = null,
      DateTimeOffset? resolvedAt = null,
      string? resolvedById = null) =>
      new(
          id, postId, userId, hostnameId, null, "mass_report_suspected", 5, 0.6,
          Details("""{"reporter_user_ids":["reporter-1"],"window_minutes":30}"""),
          resolvedAt, resolvedById, resolution, DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

  public static VoteIntegrityFlag VoteFlag(
      string id,
      string? resolution = null,
      string? postId = "post-1",
      string? topicId = null,
      string? hostnameId = null,
      DateTimeOffset? resolvedAt = null,
      string? resolvedById = null) =>
      new(
          id, postId, topicId, hostnameId, null, null, null, "velocity_spike",
          Details("""{"vote_count":20}"""), resolvedAt, resolvedById, resolution,
          DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

  public static ReportIntegrityFlagsResponse ReportPage(
      IReadOnlyList<ReportIntegrityFlag> flags,
      string? cursor = null,
      bool hasMore = false) =>
      new(flags, new PageInfo(cursor, hasMore, flags.FirstOrDefault()?.Id));

  public static VoteIntegrityFlagsResponse VotePage(
      IReadOnlyList<VoteIntegrityFlag> flags,
      string? cursor = null,
      bool hasMore = false) =>
      new(flags, new PageInfo(cursor, hasMore, flags.FirstOrDefault()?.Id));

  private static IReadOnlyDictionary<string, JsonElement> Details(string json) =>
      JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ??
          throw new InvalidOperationException("Test details did not decode.");
}

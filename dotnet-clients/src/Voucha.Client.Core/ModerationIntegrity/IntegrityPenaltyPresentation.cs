using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed record IntegrityPenaltyRow(
    string Id,
    string UserId,
    string Reason,
    string? SourceFlagId,
    string? CreatedById,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    string? RevokedById,
    double? Multiplier);

internal static class IntegrityPenaltyPresentation
{
  public static IntegrityPenaltyRow Report(ReportAbusePenalty penalty) => new(
      penalty.Id,
      penalty.UserId,
      penalty.Reason ?? string.Empty,
      penalty.SourceFlagId,
      penalty.CreatedById,
      penalty.CreatedAt ?? default,
      penalty.RevokedAt,
      penalty.RevokedById,
      null);

  public static IntegrityPenaltyRow Vote(VoteWeightPenalty penalty) => new(
      penalty.Id,
      penalty.UserId,
      penalty.Reason,
      penalty.SourceFlagId,
      penalty.CreatedById,
      penalty.CreatedAt,
      penalty.RevokedAt,
      penalty.RevokedById,
      penalty.PenaltyMultiplier);
}

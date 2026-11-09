using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

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
    double? Multiplier,
    UiText UserLabel = default,
    UiText CreatorLabel = default,
    UiText RevokerLabel = default,
    string? SourceFlagType = null);

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
      null,
      Person(penalty.UserDisplayName, penalty.UserUsername),
      Person(penalty.CreatorDisplayName, penalty.CreatorUsername),
      Person(penalty.RevokerDisplayName, penalty.RevokerUsername),
      penalty.SourceFlagType);

  public static IntegrityPenaltyRow Vote(VoteWeightPenalty penalty) => new(
      penalty.Id,
      penalty.UserId,
      penalty.Reason,
      penalty.SourceFlagId,
      penalty.CreatedById,
      penalty.CreatedAt,
      penalty.RevokedAt,
      penalty.RevokedById,
      penalty.PenaltyMultiplier,
      Person(penalty.UserDisplayName, penalty.UserUsername),
      Person(penalty.CreatorDisplayName, penalty.CreatorUsername),
      Person(penalty.RevokerDisplayName, penalty.RevokerUsername),
      penalty.SourceFlagType);

  private static UiText Person(string? displayName, string? username) =>
      !string.IsNullOrWhiteSpace(displayName) ? UiText.UserContent(displayName.Trim()) :
      !string.IsNullOrWhiteSpace(username) ? UiText.UserContent(username.Trim()) :
      UiText.Localized(UiMessageKey.NativeDotnetModerationRebasedTargetUnavailable);
}

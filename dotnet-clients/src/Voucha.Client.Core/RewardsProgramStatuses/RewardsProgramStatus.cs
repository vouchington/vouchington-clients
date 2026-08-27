using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public sealed record RewardsProgramStatus(
    string Id, string RewardsProgramStatusId, RewardsProgramSummary RewardsProgram, DateOnly? Since, DateOnly? Until)
{
  internal static RewardsProgramStatus FromWire(RewardsProgramStatusWire wire)
  {
    ArgumentNullException.ThrowIfNull(wire);
    return new(wire.Id, wire.RewardsProgramStatusId, wire.RewardsProgram, wire.Since, wire.Until);
  }
}

public sealed record RewardsProgramStatusOption(string Id, string Name, string Slug, string TopicType);
public sealed record RewardsProgramStatusPage(IReadOnlyList<RewardsProgramStatus> Results, PageInfo PageInfo);
public sealed record RewardsProgramStatusRow(RewardsProgramStatus Value, IUiLocalization Localization)
{
  public string Id => Value.Id;
  public bool IsMutating { get; init; }
  public bool CanMutate => !IsMutating;
  public string LocalizedName => Localization.Resolve(UiText.UserContent(Value.RewardsProgram.Name));
  public string? LocalizedSince => DateDetail(
      UiMessageKey.ExtractedRewardsProgramStatusesManagerStatusSummarySinceDate4a6fc195,
      Value.Since);
  public string? LocalizedUntil => DateDetail(
      UiMessageKey.ExtractedRewardsProgramStatusesManagerStatusSummaryUntilDate0bce791c,
      Value.Until);

  private string? DateDetail(UiMessageKey key, DateOnly? date) => date is { } value
      ? Localization.Format(
          key,
          ("date", UiText.Verbatim(value.ToString("d", Localization.Culture))))
      : null;
}

public sealed record RewardsProgramStatusOptionRow(RewardsProgramStatusOption Value, IUiLocalization Localization)
{
  public string LocalizedName => Localization.Resolve(UiText.UserContent(Value.Name));
}

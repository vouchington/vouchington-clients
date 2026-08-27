using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PointValuations;

public sealed record PointValuationRow(PointValuation Value, IUiLocalization Localization)
{
  public string Id => Value.Id;
  public string LocalizedName => Localization.Resolve(UiText.UserContent(Value.RewardsProgram.Name));
  public string LocalizedValue => Localization.Format(
      UiMessageKey.ExtractedPointValuationsManagerValuationSummaryValuePerPoint7111fb3c,
      ("value", UiText.Verbatim(Localization.FormatCurrency(
          Value.ValuePerPoint.ToMajorUnits(),
          Value.ValuePerPoint.Currency,
          Value.ValuePerPoint.Scale))));
  public string? LocalizedNote => Value.Note is { } note ? Localization.Resolve(UiText.UserContent(note)) : null;
}

public sealed record RewardsProgramRow(RewardsProgramOption Value, IUiLocalization Localization)
{
  public string LocalizedName => Localization.Resolve(UiText.UserContent(Value.Name));
}

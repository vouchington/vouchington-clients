using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.SpendingCategories;

public sealed record SpendingCategoryRow(SpendingCategory Value, IUiLocalization Localization)
{
  public string Id => Value.Id;
  public string LocalizedName => Localization.Resolve(UiText.UserContent(Value.Topic.Name));
  public string LocalizedAmount
  {
    get
    {
      var exponent = Api.Currency.MinorUnitExponentFor(Value.Amount.Currency) ?? 0;
      return Localization.FormatCurrency(
          Value.Amount.ToKnownCurrencyMajorUnits() ?? Value.Amount.Amount,
          Value.Amount.Currency,
          exponent,
          exponent);
    }
  }
  public string LocalizedAmountAndFrequency => $"{LocalizedAmount} {Localization.Localize(Value.SpendingFrequency == "monthly"
      ? UiMessageKey.ExtractedSpendingCategoriesManagerFrequencySelectMonthly9b11f6b7
      : UiMessageKey.ExtractedSpendingCategoriesManagerFrequencySelectAnnually1ec9d1d5)}";
  public string? LocalizedNote => Value.Note is { } note ? Localization.Resolve(UiText.UserContent(note)) : null;
  public bool CanManage => Value.CanManage;
  public bool IsReadOnlyHousehold => !CanManage && Value.OwnerType == "household";
}
public sealed record SpendingCategoryOptionRow(SpendingCategoryOption Value, IUiLocalization Localization)
{ public string LocalizedName => Localization.Resolve(UiText.UserContent(Value.Name)); }

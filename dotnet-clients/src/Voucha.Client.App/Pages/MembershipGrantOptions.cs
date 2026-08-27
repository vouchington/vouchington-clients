using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal sealed record MembershipGrantPlanOption(MembershipGrantPlanSlug Value, string Label);

internal sealed record MembershipGrantSkuOption(MembershipSku Value, string Label)
{
  public static MembershipGrantSkuOption Create(MembershipSku sku, IUiLocalization localization)
  {
    var amount = sku.Price.ToKnownCurrencyMajorUnits();
    var price = amount is { } value
        ? localization.FormatCurrency(value, sku.Price.Currency)
        : localization.Localize(UiMessageKey.NativeSwiftMembershipPriceUnavailable);
    var interval = sku.Interval switch
    {
      "monthly" => localization.Localize(UiMessageKey.NativeSwiftMembershipMonthly),
      "yearly" => localization.Localize(UiMessageKey.NativeSwiftMembershipYearly),
      _ => sku.Interval,
    };
    return new(sku, localization.Format(
        UiMessageKey.NativeSwiftMembershipSkuPriceSummary,
        ("sku", UiText.ProtocolValue(sku.Id)),
        ("price", UiText.Verbatim($"{price} {interval}"))));
  }
}

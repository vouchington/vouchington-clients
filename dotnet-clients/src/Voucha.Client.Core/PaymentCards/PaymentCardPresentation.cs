using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PaymentCards;

public sealed record PaymentCardRow(PaymentCard Value, UiText NameText, IUiLocalization Localization)
{
  public string Id => Value.Id;
  public string LocalizedName => Localization.Resolve(NameText);
  public string? LocalizedCreditLimitDetail => Value.CreditLimit is { } amount
      ? Detail(
          UiMessageKey.NativeDotnetPaymentCardsCreditLimitDetail,
          amount.ToKnownCurrencyMajorUnits() is { } majorUnits &&
              Currency.MinorUnitExponentFor(amount.Currency) is { } exponent
              ? Localization.FormatCurrency(
                  majorUnits,
                  amount.Currency,
                  exponent,
                  exponent)
              : Localization.FormatNumber(amount.Amount))
      : null;
  public string? LocalizedOpenedOnDetail => DateDetail(UiMessageKey.NativeDotnetPaymentCardsOpenedOnDetail, Value.OpenedOn);
  public string? LocalizedClosedOnDetail => DateDetail(UiMessageKey.NativeDotnetPaymentCardsClosedOnDetail, Value.ClosedOn);
  public string? LocalizedBonusOnDetail => DateDetail(UiMessageKey.NativeDotnetPaymentCardsBonusOnDetail, Value.ReceivedSignUpBonusOn);
  public string? LocalizedNoteDetail => Value.Note is { } note
      ? Localization.Format(
          UiMessageKey.NativeDotnetPaymentCardsNoteDetail,
          ("value", UiText.UserContent(note)))
      : null;
  public string? LocalizedAuthorizedUserDetail => Value.AuthorizedUserOfCard is { } parent
      ? Localization.Format(
          UiMessageKey.NativeDotnetPaymentCardsParentCardDetail,
          ("value", UiText.UserContent(parent.Card.Name)))
      : Value.IsAuthorizedUser
          ? Localization.Localize(UiMessageKey.NativeDotnetPaymentCardsAuthorizedUserWithoutParent)
          : null;

  internal static PaymentCardRow From(PaymentCard value, IUiLocalization localization) =>
      new(value, UiText.UserContent(value.Card.Name), localization);

  private string? DateDetail(UiMessageKey key, DateOnly? date) => date is { } value
      ? Detail(key, value.ToString("d", Localization.Culture)) : null;

  private string Detail(UiMessageKey key, string value) =>
      Localization.Format(key, ("value", UiText.Verbatim(value)));
}

public sealed record PaymentCardOption(
    string? Id,
    UiText NameText,
    bool IsClosed,
    IUiLocalization Localization)
{
  public string LocalizedName => Localization.Resolve(NameText);

  internal static PaymentCardOption From(PaymentCard card, IUiLocalization localization) =>
      new(card.Id, UiText.UserContent(card.Card.Name), card.ClosedOn is not null, localization);

  internal static PaymentCardOption From(PaymentCardParentSummary parent, IUiLocalization localization) =>
      new(parent.Id, UiText.UserContent(parent.Card.Name), parent.ClosedOn is not null, localization);

  internal static PaymentCardOption None(IUiLocalization localization) =>
      new(null, UiText.Localized(UiMessageKey.NativeDotnetPaymentCardsNone), false, localization);
}

public sealed record PaymentCardTopicOption(PaymentCardTopic Value, UiText NameText, IUiLocalization Localization)
{
  public string LocalizedName => Localization.Resolve(NameText);
  internal static PaymentCardTopicOption From(PaymentCardTopic topic, IUiLocalization localization) =>
      new(topic, UiText.UserContent(topic.Name), localization);
}

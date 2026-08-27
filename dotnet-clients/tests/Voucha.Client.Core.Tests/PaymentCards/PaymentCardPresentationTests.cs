using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PaymentCards;
using Xunit;

namespace Voucha.Client.Core.Tests.PaymentCards;

public sealed class PaymentCardPresentationTests
{
  [Fact]
  public void CreditLimitUsesTheCurrencyMinorUnitExponent()
  {
    var card = new PaymentCard(
        "card-1",
        "topic-1",
        null,
        null,
        null,
        new Money(1234, "jpy"),
        false,
        null,
        null,
        new PaymentCardTopic("topic-1", "Card", "card"),
        null);

    var row = PaymentCardRow.From(card, UiLocalization.English);

    Assert.Equal("Credit limit: JPY\u00A01,234", row.LocalizedCreditLimitDetail);
  }
}

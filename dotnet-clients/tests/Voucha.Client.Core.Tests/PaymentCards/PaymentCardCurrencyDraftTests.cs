using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.PaymentCards;
using Xunit;

namespace Voucha.Client.Core.Tests.PaymentCards;

public sealed class PaymentCardCurrencyDraftTests
{
  [Fact]
  public void ChangingCreditLimitCurrencyToJpyUsesZeroExponent()
  {
    var card = new PaymentCard(
        "card", "topic", null, null, null, new Money(1250, "usd"), false, null, null,
        new PaymentCardTopic("topic", "Example", "example"), null);
    var draft = new PaymentCardDraft(card, CultureInfo.InvariantCulture)
    {
      CreditLimitCurrency = "jpy",
      CreditLimit = "123",
    };
    Assert.True(draft.TryParseCreditLimit(out var value));
    Assert.Equal(new Money(123, "jpy"), value);
  }
}

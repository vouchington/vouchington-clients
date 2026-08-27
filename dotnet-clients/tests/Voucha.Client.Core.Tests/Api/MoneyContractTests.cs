using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class MoneyContractTests
{
  [Fact]
  public void ValidatesJsonSafeAmountsCurrencyCodesAndScale()
  {
    var valid = JsonSerializer.Deserialize<Money>(
        """{"amount":9007199254740991,"currency":"usd"}""", VouchaApiJson.Options);
    Assert.Equal(MoneyContract.MaximumAmount, valid!.Amount);
    Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Money>(
        """{"amount":-1,"currency":"usd"}""", VouchaApiJson.Options));
    Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Money>(
        """{"amount":1,"currency":"USD"}""", VouchaApiJson.Options));
    Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<ScaledMoney>(
        """{"amount":35000,"currency":"usd","scale":5}""", VouchaApiJson.Options));
    Assert.Equal(0.035m, new ScaledMoney(35_000, "usd").ToMajorUnits());
  }

  [Fact]
  public void ScaledMoneyAggregatePreservesArbitraryCanonicalDigits()
  {
    var aggregate = JsonSerializer.Deserialize<ScaledMoneyAggregate>(
        """{"amount":"180143985094819820000","currency":"usd","scale":6}""",
        VouchaApiJson.Options);
    Assert.Equal("180143985094819820000", aggregate!.Amount);
    Assert.Throws<ArgumentException>(() => new ScaledMoneyAggregate("", "usd"));
    Assert.Throws<ArgumentException>(() => new ScaledMoneyAggregate("01", "usd"));
    Assert.Throws<ArgumentException>(() => new ScaledMoneyAggregate("-1", "usd"));
    Assert.Throws<ArgumentException>(() => new ScaledMoneyAggregate("1.5", "usd"));
  }

  [Fact]
  public void DecodesCurrencyCatalogAndBuildsCursorRequest()
  {
    var response = JsonSerializer.Deserialize<CurrenciesResponse>(
        ApiFixtureLoader.LoadResponse("shared.currencies.list.default"), VouchaApiJson.Options)!;
    Assert.Equal(0, response.Results.Single(currency => currency.Code == "jpy").MinorUnitExponent);
    var request = VouchaApiEndpoints.Currencies("currency cursor", 6);
    Assert.Equal("/api/v1/currencies", request.Path);
    Assert.Equal("currency cursor", request.Query["after"]);
    Assert.Equal("6", request.Query["limit"]);
  }

  [Fact]
  public void MoneyRangeRequiresIncreasingBoundsInOneCurrency()
  {
    var minimum = new Money(100, "usd");
    Assert.Null(new MoneyRange(minimum, null).Maximum);
    Assert.Throws<ArgumentException>(() => new MoneyRange(minimum, new Money(100, "usd")));
    Assert.Throws<ArgumentException>(() => new MoneyRange(minimum, new Money(200, "jpy")));
  }
}

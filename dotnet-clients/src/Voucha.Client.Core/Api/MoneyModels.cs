using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public static class MoneyContract
{
  public const long MaximumAmount = 9_007_199_254_740_991;

  internal static void ValidateAmount(long amount)
  {
    if (amount is < 0 or > MaximumAmount)
      throw new ArgumentOutOfRangeException(nameof(amount));
  }

  internal static void ValidateCurrency(string currency)
  {
    ArgumentNullException.ThrowIfNull(currency);
    if (currency.Length != 3 || currency.Any(character => character is < 'a' or > 'z'))
      throw new ArgumentException("Currency codes must be three lowercase ASCII letters.", nameof(currency));
  }
}

public sealed record Currency
{
  public static IReadOnlyList<Currency> Supported { get; } =
  [
    new("aud", 2), new("cad", 2), new("eur", 2), new("gbp", 2), new("jpy", 0), new("usd", 2),
  ];
  [JsonConstructor]
  public Currency(string code, int minorUnitExponent)
  {
    MoneyContract.ValidateCurrency(code);
    if (minorUnitExponent is < 0 or > 4)
      throw new ArgumentOutOfRangeException(nameof(minorUnitExponent));
    Code = code;
    MinorUnitExponent = minorUnitExponent;
  }

  [JsonPropertyName("code")]
  public string Code { get; }

  [JsonPropertyName("minor_unit_exponent")]
  public int MinorUnitExponent { get; }

  public static int? MinorUnitExponentFor(string code) =>
      Supported.FirstOrDefault(currency => currency.Code == code)?.MinorUnitExponent;
}

public sealed record Money
{
  [JsonConstructor]
  public Money(long amount, string currency)
  {
    MoneyContract.ValidateAmount(amount);
    MoneyContract.ValidateCurrency(currency);
    Amount = amount;
    Currency = currency;
  }

  [JsonPropertyName("amount")]
  public long Amount { get; }

  [JsonPropertyName("currency")]
  public string Currency { get; }

  public decimal ToMajorUnits(int minorUnitExponent) =>
      Amount / Pow10(minorUnitExponent);

  public decimal? ToKnownCurrencyMajorUnits() =>
      global::Voucha.Client.Core.Api.Currency.MinorUnitExponentFor(Currency) is { } exponent
          ? ToMajorUnits(exponent)
          : null;

  internal static decimal Pow10(int exponent)
  {
    if (exponent is < 0 or > 28) throw new ArgumentOutOfRangeException(nameof(exponent));
    decimal result = 1;
    for (var index = 0; index < exponent; index++) result *= 10;
    return result;
  }
}

public sealed record ScaledMoney
{
  public const int RequiredScale = 6;

  [JsonConstructor]
  public ScaledMoney(long amount, string currency, int scale = RequiredScale)
  {
    MoneyContract.ValidateAmount(amount);
    MoneyContract.ValidateCurrency(currency);
    ArgumentOutOfRangeException.ThrowIfNotEqual(scale, RequiredScale);
    Amount = amount;
    Currency = currency;
    Scale = scale;
  }

  [JsonPropertyName("amount")]
  public long Amount { get; }

  [JsonPropertyName("currency")]
  public string Currency { get; }

  [JsonPropertyName("scale")]
  public int Scale { get; }

  public decimal ToMajorUnits() => Amount / Money.Pow10(RequiredScale);
}

public sealed record ScaledMoneyAggregate
{
  public const int RequiredScale = 6;

  [JsonConstructor]
  public ScaledMoneyAggregate(string amount, string currency, int scale = RequiredScale)
  {
    ArgumentException.ThrowIfNullOrEmpty(amount);
    if ((amount.Length > 1 && amount[0] == '0') || amount.Any(character => character is < '0' or > '9'))
      throw new ArgumentException("Aggregate amount must be a canonical non-negative integer.", nameof(amount));
    MoneyContract.ValidateCurrency(currency);
    ArgumentOutOfRangeException.ThrowIfNotEqual(scale, RequiredScale);
    Amount = amount;
    Currency = currency;
    Scale = scale;
  }

  [JsonPropertyName("amount")]
  public string Amount { get; }

  [JsonPropertyName("currency")]
  public string Currency { get; }

  [JsonPropertyName("scale")]
  public int Scale { get; }

  public BigInteger ScaledAmount => BigInteger.Parse(Amount, CultureInfo.InvariantCulture);
}

public sealed record MoneyRange
{
  [JsonConstructor]
  public MoneyRange(Money minimum, Money? maximum)
  {
    ArgumentNullException.ThrowIfNull(minimum);
    if (maximum is not null &&
        (maximum.Currency != minimum.Currency || maximum.Amount <= minimum.Amount))
      throw new ArgumentException("Money range bounds must increase within one currency.", nameof(maximum));
    Minimum = minimum;
    Maximum = maximum;
  }

  [JsonPropertyName("minimum")]
  public Money Minimum { get; }

  [JsonPropertyName("maximum")]
  public Money? Maximum { get; }
}

public sealed record CurrenciesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<Currency> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

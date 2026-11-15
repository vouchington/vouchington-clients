using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed record McpMoney(decimal Major, string Currency, int Exponent)
{
  internal string Format(IUiLocalization localization) =>
      localization.FormatCurrency(Major, Currency, Exponent, Exponent);
}

/// <summary>
/// The member's own displayable financial fields. <c>individual_id</c>, <c>currency</c> and
/// <c>updated_at</c> are never read.
/// </summary>
public sealed record McpFinancialProfileFact(
    string? CreditScoreRange,
    McpMoney? IncomeMinimum,
    McpMoney? IncomeMaximum,
    McpMoney? TotalCreditLimit,
    decimal? YearsOfCreditHistory,
    decimal? HardInquiries12m,
    decimal? CardsOpened24m) : McpResultFact
{
  internal bool IsEmpty => CreditScoreRange is null && IncomeMinimum is null && TotalCreditLimit is null &&
      YearsOfCreditHistory is null && HardInquiries12m is null && CardsOpened24m is null;

  internal override IReadOnlyList<string> Lines(IUiLocalization localization)
  {
    var lines = new List<string>();
    if (CreditScoreRange is not null)
      lines.Add(Row(localization, UiMessageKey.NativeMcpFactsFinancialProfileCreditScoreRange, CreditScoreRange));
    if (IncomeMinimum is not null)
      lines.Add(Row(localization, UiMessageKey.NativeMcpFactsFinancialProfileStatedIncomeRange,
          IncomeMaximum is null
              ? $"{IncomeMinimum.Format(localization)}+"
              : $"{IncomeMinimum.Format(localization)} – {IncomeMaximum.Format(localization)}"));
    if (TotalCreditLimit is not null)
      lines.Add(Row(localization, UiMessageKey.NativeMcpFactsFinancialProfileTotalCreditLimit,
          TotalCreditLimit.Format(localization)));
    Count(lines, localization, UiMessageKey.NativeMcpFactsFinancialProfileYearsOfCreditHistory, YearsOfCreditHistory);
    Count(lines, localization, UiMessageKey.NativeMcpFactsFinancialProfileHardInquiries12m, HardInquiries12m);
    Count(lines, localization, UiMessageKey.NativeMcpFactsFinancialProfileCardsOpened24m, CardsOpened24m);
    return lines;
  }

  private static void Count(List<string> lines, IUiLocalization localization, UiMessageKey label, decimal? value)
  {
    if (value is { } count) lines.Add(Row(localization, label, localization.FormatNumber(count)));
  }

  private static string Row(IUiLocalization localization, UiMessageKey label, string value) =>
      $"{localization.Localize(label)}: {value}";
}

/// <summary>A member without a financial profile.</summary>
public sealed record McpEmptyFinancialProfileFact : McpResultFact
{
  internal override IReadOnlyList<string> Lines(IUiLocalization localization) =>
      [localization.Localize(UiMessageKey.NativeMcpFactsFinancialProfileEmpty)];
}

internal static class McpFinancialFacts
{
  private static readonly HashSet<string> ScoreRanges =
      new(["300-579", "580-669", "670-739", "740-799", "800-850"], StringComparer.Ordinal);

  internal static McpResultFact? Profile(JsonElement content)
  {
    if (!McpFactJson.Object(content, "result", out var result) ||
        !result.TryGetProperty("financial_profile", out var profile)) return null;
    if (profile.ValueKind == JsonValueKind.Null) return new McpEmptyFinancialProfileFact();
    if (profile.ValueKind != JsonValueKind.Object ||
        !McpFactJson.OptionalText(profile, "credit_score_range", out var score) ||
        score is not null && !ScoreRanges.Contains(score) ||
        !OptionalMoney(profile, "total_credit_limit", out var limit) ||
        !Income(profile, out var minimum, out var maximum) ||
        !Count(profile, "years_of_credit_history", out var years) ||
        !Count(profile, "hard_inquiries_12m", out var inquiries) ||
        !Count(profile, "cards_opened_24m", out var cards)) return null;
    var fact = new McpFinancialProfileFact(score, minimum, maximum, limit, years, inquiries, cards);
    return fact.IsEmpty ? new McpEmptyFinancialProfileFact() : fact;
  }

  private static bool Income(JsonElement profile, out McpMoney? minimum, out McpMoney? maximum)
  {
    minimum = maximum = null;
    if (!profile.TryGetProperty("stated_income_range", out var range)) return false;
    if (range.ValueKind == JsonValueKind.Null) return true;
    if (range.ValueKind != JsonValueKind.Object ||
        !range.TryGetProperty("minimum", out var low) || ReadMoney(low) is not { } min ||
        !range.TryGetProperty("maximum", out var high)) return false;
    // The producer sends maximum: null for an open-ended range.
    if (high.ValueKind == JsonValueKind.Null)
    {
      minimum = min;
      return true;
    }
    if (ReadMoney(high) is not { } max || min.Currency != max.Currency || min.Major > max.Major) return false;
    minimum = min;
    maximum = max;
    return true;
  }

  private static bool OptionalMoney(JsonElement source, string name, out McpMoney? money)
  {
    money = null;
    if (!source.TryGetProperty(name, out var value)) return false;
    if (value.ValueKind == JsonValueKind.Null) return true;
    money = ReadMoney(value);
    return money is not null;
  }

  private static McpMoney? ReadMoney(JsonElement value)
  {
    if (value.ValueKind != JsonValueKind.Object ||
        !value.TryGetProperty("amount", out var amount) || !amount.TryGetInt64(out var minor) ||
        minor is < 0 or > MoneyContract.MaximumAmount ||
        !McpFactJson.Text(value, "currency", out var currency) ||
        Api.Currency.MinorUnitExponentFor(currency) is not { } exponent) return null;
    return new McpMoney(new Api.Money(minor, currency).ToMajorUnits(exponent), currency.ToUpperInvariant(), exponent);
  }

  private static bool Count(JsonElement source, string name, out decimal? value)
  {
    value = null;
    if (!source.TryGetProperty(name, out var property)) return false;
    if (property.ValueKind == JsonValueKind.Null) return true;
    if (property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out var number) || number < 0)
      return false;
    value = number;
    return true;
  }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Agent;

public sealed record MemberFinancialProfile
{
  [JsonRequired, JsonPropertyName("individual_id")]
  public string IndividualId { get; init; } = "";

  [JsonRequired, JsonPropertyName("credit_score_range")]
  public string? CreditScoreRange { get; init; }

  [JsonRequired, JsonPropertyName("stated_income_range")]
  public MoneyRange? StatedIncomeRange { get; init; }

  [JsonRequired, JsonPropertyName("total_credit_limit")]
  public Money? TotalCreditLimit { get; init; }

  [JsonRequired, JsonPropertyName("currency")]
  public string Currency { get; init; } = "";

  [JsonRequired, JsonPropertyName("years_of_credit_history")]
  public double? YearsOfCreditHistory { get; init; }

  [JsonRequired, JsonPropertyName("hard_inquiries_12m")]
  public double? HardInquiries12m { get; init; }

  [JsonRequired, JsonPropertyName("cards_opened_24m")]
  public double? CardsOpened24m { get; init; }

  [JsonRequired, JsonPropertyName("updated_at")]
  public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record MemberFinancialProfileResult
{
  [JsonRequired, JsonPropertyName("financial_profile")]
  public MemberFinancialProfile? FinancialProfile { get; init; }
}

public static class MemberMcpFinancialProfileExtensions
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true
  };

  public static async Task<MemberFinancialProfileResult> GetMyFinancialProfileAsync(
      this MemberMcpAuthorizedClient client, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(client);
    using var arguments = JsonDocument.Parse("{}");
    var response = await client.CallToolAsync(
        "get_my_financial_profile", arguments.RootElement, cancellationToken).ConfigureAwait(false);
    if (response.IsError == true) throw new InvalidDataException("The financial profile tool failed.");
    if (response.StructuredContent is not { } content)
      throw new InvalidDataException("The financial profile tool returned no structured content.");
    var envelope = content.Deserialize<FinancialProfileEnvelope>(JsonOptions);
    if (envelope?.Success != true || envelope.Result is null)
      throw new InvalidDataException("The financial profile tool returned an invalid result.");
    return envelope.Result;
  }

  private sealed record FinancialProfileEnvelope
  {
    [JsonRequired, JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonRequired, JsonPropertyName("result")]
    public MemberFinancialProfileResult? Result { get; init; }
  }
}

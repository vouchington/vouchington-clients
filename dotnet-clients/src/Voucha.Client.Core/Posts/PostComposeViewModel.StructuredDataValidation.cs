using System.Text.Json;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private static readonly HashSet<string> CreditCardResults =
      ["approved", "denied", "pending", "counter_offer", "retention_offer", "sign_up_bonus", "offer"];

  private static readonly HashSet<string> BankAccountResults =
      ["approved", "denied", "sign_up_bonus", "offer"];

  private static readonly HashSet<string> CreditScoreRanges =
      ["300-579", "580-669", "670-739", "740-799", "800-850"];

  private bool TryParseStructuredData(out JsonElement? element)
  {
    element = null;
    if (string.IsNullOrWhiteSpace(StructuredDataJson)) return true;
    try
    {
      using var document = JsonDocument.Parse(StructuredDataJson);
      element = document.RootElement.Clone();
      return true;
    }
    catch (JsonException)
    {
      return false;
    }
  }

  private bool HasValidStructuredData()
  {
    if (!TryParseStructuredData(out var parsed) || parsed is null) return false;
    var data = parsed.Value;
    if (data.ValueKind != JsonValueKind.Object) return false;
    var vertical = EmptyToNull(DataPointVertical);
    return vertical switch
    {
      "credit_card" => HasValidCreditCardData(data),
      "bank_account" => HasValidBankAccountData(data),
      _ => false,
    };
  }

  private static bool HasValidCreditCardData(JsonElement data) =>
      HasStringValue(data, "vertical", "credit_card") &&
      HasSchemaVersion(data) &&
      HasValidTopicIds(data) &&
      HasAllowedStringValue(data, "result", CreditCardResults) &&
      HasAllowedStringValue(data, "credit_score_range", CreditScoreRanges);

  private static bool HasValidBankAccountData(JsonElement data) =>
      HasStringValue(data, "vertical", "bank_account") &&
      HasSchemaVersion(data) &&
      HasValidTopicIds(data) &&
      HasAllowedStringValue(data, "result", BankAccountResults);

  private static bool HasSchemaVersion(JsonElement data) =>
      data.TryGetProperty("schema_version", out var property) &&
      property.ValueKind == JsonValueKind.Number &&
      property.TryGetInt32(out var value) &&
      value == 1;

  private static bool HasValidTopicIds(JsonElement data)
  {
    if (!data.TryGetProperty("topic_ids", out var property) ||
        property.ValueKind != JsonValueKind.Array ||
        property.GetArrayLength() is < 1 or > 5)
    {
      return false;
    }

    var topicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in property.EnumerateArray())
    {
      if (item.ValueKind != JsonValueKind.String ||
          EmptyToNull(item.GetString()) is not { } topicId ||
          !Guid.TryParse(topicId, out _) ||
          !topicIds.Add(topicId))
      {
        return false;
      }
    }
    return true;
  }

  private static bool HasStringValue(JsonElement data, string propertyName, string expected) =>
      data.TryGetProperty(propertyName, out var property) &&
      property.ValueKind == JsonValueKind.String &&
      string.Equals(property.GetString(), expected, StringComparison.Ordinal);

  private static bool HasAllowedStringValue(
      JsonElement data,
      string propertyName,
      HashSet<string> allowedValues) =>
      data.TryGetProperty(propertyName, out var property) &&
      property.ValueKind == JsonValueKind.String &&
      EmptyToNull(property.GetString()) is { } value &&
      allowedValues.Contains(value);
}

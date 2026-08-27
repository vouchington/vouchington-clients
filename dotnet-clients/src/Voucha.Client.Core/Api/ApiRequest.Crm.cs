using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CrmContactsRequest(
    string? Query = null,
    string? Status = null,
    string? Vertical = null,
    bool? Linked = null,
    string? After = null,
    int Limit = 25);

public sealed record CreateCrmContactBody(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("phone")] string? Phone = null,
    [property: JsonPropertyName("vertical")] CrmContactVertical? Vertical = null,
    [property: JsonPropertyName("contact_type")] CrmContactType? ContactType = null,
    [property: JsonPropertyName("follower_count")] int? FollowerCount = null,
    [property: JsonPropertyName("notes")] string? Notes = null);

public sealed record UpdateCrmContactBody(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("email")] string? Email = null,
    [property: JsonPropertyName("phone")] JsonNullableString? Phone = null,
    [property: JsonPropertyName("vertical")] JsonNullableCrmContactVertical? Vertical = null,
    [property: JsonPropertyName("contact_type")] CrmContactType? ContactType = null,
    [property: JsonPropertyName("follower_count")] JsonNullableInt? FollowerCount = null,
    [property: JsonPropertyName("notes")] JsonNullableString? Notes = null,
    [property: JsonPropertyName("assigned_to_id")] string? AssignedToId = null,
    [property: JsonPropertyName("contacted_at")] DateTimeOffset? ContactedAt = null,
    [property: JsonPropertyName("responded_at")] DateTimeOffset? RespondedAt = null,
    [property: JsonPropertyName("converted_at")] DateTimeOffset? ConvertedAt = null,
    [property: JsonPropertyName("opted_out_at")] DateTimeOffset? OptedOutAt = null);

public sealed record SendCrmEmailBody(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body_html")] string? BodyHtml = null,
    [property: JsonPropertyName("body_text")] string? BodyText = null,
    [property: JsonPropertyName("email_provider")] CrmEmailProvider EmailProvider = CrmEmailProvider.Ses,
    [property: JsonPropertyName("cta_url")] Uri? CtaUrl = null,
    [property: JsonPropertyName("ai_prompt")] string? AiPrompt = null,
    [property: JsonConverter(typeof(UtcIso8601DateTimeOffsetConverter))]
    [property: JsonPropertyName("ai_generated_at")] DateTimeOffset? AiGeneratedAt = null);

internal sealed class UtcIso8601DateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
  private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

  public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
      reader.TokenType == JsonTokenType.Null
          ? null
          : DateTimeOffset.Parse(reader.GetString() ?? string.Empty, CultureInfo.InvariantCulture).ToUniversalTime();

  public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
  {
    if (value is null)
    {
      writer.WriteNullValue();
      return;
    }

    writer.WriteStringValue(value.Value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
  }
}

public sealed record CreateCrmNoteBody([property: JsonPropertyName("body")] string Body);

public sealed record LinkCrmContactToUserBody([property: JsonPropertyName("user_id")] string UserId);

public sealed record GenerateCrmEmailDraftBody(
    [property: JsonPropertyName("prompt")] string? Prompt = null,
    [property: JsonPropertyName("tone")] string? Tone = null);

public sealed record ImportCrmContactsBody([property: JsonPropertyName("csv")] string Csv);

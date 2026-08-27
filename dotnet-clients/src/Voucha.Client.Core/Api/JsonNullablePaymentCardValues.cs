using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Voucha.Client.Core.Api;

[JsonConverter(typeof(JsonNullableDateConverter))]
public readonly record struct JsonNullableDate(DateOnly? Value)
{
  public static JsonNullableDate Null { get; } = new(null);
  public static JsonNullableDate FromDate(DateOnly value) => new(value);
}

public sealed class JsonNullableDateConverter : JsonConverter<JsonNullableDate>
{
  public override JsonNullableDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
      reader.TokenType == JsonTokenType.Null
          ? JsonNullableDate.Null
          : JsonNullableDate.FromDate(DateOnly.ParseExact(reader.GetString()!, "yyyy-MM-dd"));

  public override void Write(Utf8JsonWriter writer, JsonNullableDate value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);
    if (value.Value is { } date) writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    else writer.WriteNullValue();
  }
}

[JsonConverter(typeof(JsonNullableMoneyConverter))]
public readonly record struct JsonNullableMoney(Money? Value)
{
  public static JsonNullableMoney Null { get; } = new(null);
  public static JsonNullableMoney FromMoney(Money value) => new(value);
}

public sealed class JsonNullableMoneyConverter : JsonConverter<JsonNullableMoney>
{
  public override JsonNullableMoney Read(
      ref Utf8JsonReader reader,
      Type typeToConvert,
      JsonSerializerOptions options) =>
      reader.TokenType == JsonTokenType.Null
          ? JsonNullableMoney.Null
          : JsonNullableMoney.FromMoney(
              JsonSerializer.Deserialize<Money>(ref reader, options)
              ?? throw new JsonException("Expected a money object."));

  public override void Write(
      Utf8JsonWriter writer,
      JsonNullableMoney value,
      JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);
    if (value.Value is { } money) JsonSerializer.Serialize(writer, money, options);
    else writer.WriteNullValue();
  }
}

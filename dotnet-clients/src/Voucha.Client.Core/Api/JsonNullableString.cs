using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

[JsonConverter(typeof(JsonNullableStringConverter))]
public readonly record struct JsonNullableString(string? Value)
{
  public static JsonNullableString Null { get; } = new(null);

  public static JsonNullableString FromString(string value) => new(value);
}

public sealed class JsonNullableStringConverter : JsonConverter<JsonNullableString>
{
  public override JsonNullableString Read(
      ref Utf8JsonReader reader,
      Type typeToConvert,
      JsonSerializerOptions options)
  {
    return reader.TokenType == JsonTokenType.Null
        ? JsonNullableString.Null
        : JsonNullableString.FromString(reader.GetString()!);
  }

  public override void Write(Utf8JsonWriter writer, JsonNullableString value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);

    if (value.Value is null)
    {
      writer.WriteNullValue();
      return;
    }
    writer.WriteStringValue(value.Value);
  }
}

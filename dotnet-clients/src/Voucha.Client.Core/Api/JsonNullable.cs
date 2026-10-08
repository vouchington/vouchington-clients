using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

[JsonConverter(typeof(JsonNullableIntConverter))]
public readonly record struct JsonNullableInt(int? Value)
{
  public static JsonNullableInt Null { get; } = new(null);

  public static JsonNullableInt FromInt(int value) => new(value);
}

public sealed class JsonNullableIntConverter : JsonConverter<JsonNullableInt>
{
  public override JsonNullableInt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    return reader.TokenType == JsonTokenType.Null ? JsonNullableInt.Null : JsonNullableInt.FromInt(reader.GetInt32());
  }

  public override void Write(Utf8JsonWriter writer, JsonNullableInt value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);

    if (value.Value is null)
    {
      writer.WriteNullValue();
      return;
    }

    writer.WriteNumberValue(value.Value.Value);
  }
}

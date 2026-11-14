using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

internal sealed class NonNullApiStringConverter : JsonConverter<string>
{
  public NonNullApiStringConverter() { }

  public override bool HandleNull => true;

  public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.String)
    {
      throw new JsonException("The required identity must be a string.");
    }
    return reader.GetString() ?? throw new JsonException("The required identity cannot be null.");
  }

  public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
  {
    if (value is null)
    {
      throw new JsonException("The required identity cannot be null.");
    }
    writer.WriteStringValue(value);
  }
}

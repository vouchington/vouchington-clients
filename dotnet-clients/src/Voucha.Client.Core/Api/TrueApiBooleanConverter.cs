using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

internal sealed class TrueApiBooleanConverter : JsonConverter<bool>
{
  public TrueApiBooleanConverter() { }

  public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
      reader.TokenType == JsonTokenType.True ? true : throw new JsonException("Expected literal true.");

  public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);
    if (!value) throw new JsonException("Expected literal true.");
    writer.WriteBooleanValue(true);
  }
}

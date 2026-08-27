using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

[JsonConverter(typeof(DynamicConfigValueConverter))]
public abstract record DynamicConfigValue;

public sealed record DynamicConfigBooleanValue(bool Value) : DynamicConfigValue;
public sealed record DynamicConfigNumericValue(double Value) : DynamicConfigValue;
public sealed record DynamicConfigStringValue(string Value) : DynamicConfigValue;

public static class DynamicConfigValues
{
  public static DynamicConfigValue From(bool value) => new DynamicConfigBooleanValue(value);
  public static DynamicConfigValue From(double value) => new DynamicConfigNumericValue(value);
  public static DynamicConfigValue From(string value) => new DynamicConfigStringValue(value);
}

public sealed class DynamicConfigValueConverter : JsonConverter<DynamicConfigValue>
{
  public override DynamicConfigValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
      reader.TokenType switch
      {
        JsonTokenType.True => DynamicConfigValues.From(true),
        JsonTokenType.False => DynamicConfigValues.From(false),
        JsonTokenType.Number when reader.TryGetDouble(out var value) && double.IsFinite(value) => DynamicConfigValues.From(value),
        JsonTokenType.String => DynamicConfigValues.From(reader.GetString()!),
        _ => throw new JsonException("Dynamic config values must be boolean, finite number, or string."),
      };

  public override void Write(Utf8JsonWriter writer, DynamicConfigValue value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);
    switch (value)
    {
      case DynamicConfigBooleanValue boolean: writer.WriteBooleanValue(boolean.Value); break;
      case DynamicConfigNumericValue number when double.IsFinite(number.Value): writer.WriteNumberValue(number.Value); break;
      case DynamicConfigStringValue text: writer.WriteStringValue(text.Value); break;
      default: throw new JsonException("Dynamic config numbers must be finite.");
    }
  }
}

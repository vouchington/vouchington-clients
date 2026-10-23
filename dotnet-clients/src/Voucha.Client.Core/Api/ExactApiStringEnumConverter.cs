using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

internal sealed class ExactApiStringEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
  private static readonly Dictionary<string, T> Values = Enum.GetValues<T>().ToDictionary(
      value => typeof(T).GetField(value.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
          ?? throw new InvalidOperationException($"{typeof(T).Name} values must declare their exact API name."),
      value => value,
      StringComparer.Ordinal);

  public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.String && Values.TryGetValue(reader.GetString()!, out var value)) return value;
    throw new JsonException($"Expected a declared {typeof(T).Name} API string.");
  }

  public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);
    foreach (var entry in Values)
    {
      if (EqualityComparer<T>.Default.Equals(entry.Value, value))
      {
        writer.WriteStringValue(entry.Key);
        return;
      }
    }

    throw new JsonException($"Expected a declared {typeof(T).Name} API value.");
  }
}

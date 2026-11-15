using System.Text.Json;

namespace Voucha.Client.Core.Chat;

/// <summary>Strict readers for validated MCP result objects; any mismatch rejects the whole fact.</summary>
internal static class McpFactJson
{
  internal static bool Object(JsonElement source, string name, out JsonElement value) =>
      Property(source, name, JsonValueKind.Object, out value);

  internal static bool Array(JsonElement source, string name, out JsonElement value) =>
      Property(source, name, JsonValueKind.Array, out value);

  internal static bool Text(JsonElement source, string name, out string value)
  {
    value = "";
    if (!Property(source, name, JsonValueKind.String, out var property) ||
        property.GetString() is not { } text) return false;
    value = text;
    return true;
  }

  internal static bool Bool(JsonElement source, string name, out bool value)
  {
    value = false;
    if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var property) ||
        property.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
    value = property.GetBoolean();
    return true;
  }

  /// <summary>A string, an explicit null, or an absent property; anything else is invalid.</summary>
  internal static bool OptionalText(JsonElement source, string name, out string? value)
  {
    value = null;
    if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var property))
      return source.ValueKind == JsonValueKind.Object;
    if (property.ValueKind == JsonValueKind.Null) return true;
    if (property.ValueKind != JsonValueKind.String) return false;
    value = property.GetString();
    return true;
  }

  internal static bool Displayable(string? value) => !string.IsNullOrWhiteSpace(value);

  private static bool Property(JsonElement source, string name, JsonValueKind kind, out JsonElement value)
  {
    value = default;
    if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var property) ||
        property.ValueKind != kind) return false;
    value = property;
    return true;
  }
}

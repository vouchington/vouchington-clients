using System.Text.Json;
using System.Text;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  private static bool Check(JsonElement value, JsonElement schema, JsonElement root, int depth)
  {
    if (depth >= MaxDepth) throw new InvalidDataException("MCP schema reference depth exceeded.");
    if (schema.TryGetProperty("$ref", out var reference) &&
        !Check(value, Reference(reference, root), root, depth + 1)) return false;
    if (schema.TryGetProperty("type", out var type) && !MatchesType(value, type)) return false;
    if (schema.TryGetProperty("const", out var constant) && !Same(value, constant)) return false;
    if (schema.TryGetProperty("enum", out var choices) &&
        !choices.EnumerateArray().Any(choice => Same(value, choice))) return false;
    foreach (var keyword in new[] { "allOf", "anyOf", "oneOf" })
    {
      if (!schema.TryGetProperty(keyword, out var branches)) continue;
      var matches = branches.EnumerateArray().Count(branch => Check(value, branch, root, depth + 1));
      if (keyword == "allOf" && matches != branches.GetArrayLength() ||
          keyword == "anyOf" && matches == 0 || keyword == "oneOf" && matches != 1) return false;
    }
    return value.ValueKind switch
    {
      JsonValueKind.Object => CheckObject(value, schema, root, depth),
      JsonValueKind.Array => CheckArray(value, schema, root, depth),
      JsonValueKind.String => CheckString(value.GetString()!, schema),
      JsonValueKind.Number => CheckNumber(value, schema),
      _ => true,
    };
  }

  private static bool MatchesType(JsonElement value, JsonElement type)
  {
    if (type.ValueKind == JsonValueKind.Array)
      return type.EnumerateArray().Any(candidate => MatchesType(value, candidate));
    return type.GetString() switch
    {
      "object" => value.ValueKind == JsonValueKind.Object,
      "array" => value.ValueKind == JsonValueKind.Array,
      "string" => value.ValueKind == JsonValueKind.String,
      "integer" => Number(value, out var number) && decimal.Truncate(number) == number,
      "number" => value.ValueKind == JsonValueKind.Number,
      "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
      "null" => value.ValueKind == JsonValueKind.Null,
      _ => false,
    };
  }

  private static bool CheckObject(JsonElement value, JsonElement schema, JsonElement root, int depth)
  {
    var fields = value.EnumerateObject().ToArray();
    if (!Within(fields.Length, schema, "minProperties", "maxProperties")) return false;
    if (schema.TryGetProperty("required", out var required) &&
        !required.EnumerateArray().All(name => value.TryGetProperty(name.GetString()!, out _))) return false;
    var hasProperties = schema.TryGetProperty("properties", out var properties);
    var hasAdditional = schema.TryGetProperty("additionalProperties", out var additional);
    foreach (var field in fields)
    {
      if (hasProperties && properties.TryGetProperty(field.Name, out var declared))
      {
        if (!Check(field.Value, declared, root, depth + 1)) return false;
      }
      else if (hasAdditional)
      {
        if (additional.ValueKind == JsonValueKind.False ||
            additional.ValueKind == JsonValueKind.Object &&
            !Check(field.Value, additional, root, depth + 1)) return false;
      }
    }
    return true;
  }

  private static bool CheckArray(JsonElement value, JsonElement schema, JsonElement root, int depth)
  {
    var items = value.EnumerateArray().ToArray();
    if (!Within(items.Length, schema, "minItems", "maxItems")) return false;
    if (schema.TryGetProperty("uniqueItems", out var unique) && unique.ValueKind == JsonValueKind.True)
      for (var i = 0; i < items.Length; i++)
        for (var j = i + 1; j < items.Length; j++)
          if (Same(items[i], items[j])) return false;
    return !schema.TryGetProperty("items", out var itemSchema) ||
        items.All(item => Check(item, itemSchema, root, depth + 1));
  }

  private static bool CheckString(string text, JsonElement schema)
  {
    if (!Within(text.EnumerateRunes().Count(), schema, "minLength", "maxLength")) return false;
    if (schema.TryGetProperty("format", out var format))
    {
      if (format.GetString() == "uuid" && !Guid.TryParse(text, out _)) return false;
      if (format.GetString() == "uri" && !Uri.TryCreate(text, UriKind.Absolute, out _)) return false;
    }
    if (schema.TryGetProperty("pattern", out var pattern))
    {
      try { if (!BoundedPattern(pattern.GetString()!).IsMatch(text)) return false; }
      catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { return false; }
    }
    return true;
  }

  private static bool CheckNumber(JsonElement value, JsonElement schema)
  {
    if (!schema.TryGetProperty("minimum", out var minimum) &&
        !schema.TryGetProperty("maximum", out _)) return true;
    if (!Number(value, out var number)) return false;
    if (schema.TryGetProperty("minimum", out minimum) && Number(minimum, out var lower) && number < lower)
      return false;
    if (schema.TryGetProperty("maximum", out var maximum) && Number(maximum, out var upper) && number > upper)
      return false;
    return true;
  }

  private static bool Within(int count, JsonElement schema, string minimum, string maximum) =>
      (!schema.TryGetProperty(minimum, out var min) || Count(min, out var lower) && count >= lower) &&
      (!schema.TryGetProperty(maximum, out var max) || Count(max, out var upper) && count <= upper);
}

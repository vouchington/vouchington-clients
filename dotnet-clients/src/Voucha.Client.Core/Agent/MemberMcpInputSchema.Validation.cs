using System.Text.Json;
using System.Text;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  private static bool Check(JsonElement value, JsonElement schema, JsonElement root, int depth, ref int remainingWork, bool output = false, bool skipRootAnyOf = false)
  {
    if (depth >= MaxDepth || remainingWork-- <= 0)
      throw new InvalidDataException("MCP schema validation work exceeded.");
    if (output && value.ValueKind == JsonValueKind.Number)
    {
      var length = value.GetRawText().Length;
      if (length > remainingWork) throw new InvalidDataException("MCP schema validation work exceeded.");
      remainingWork -= length;
    }
    if (schema.TryGetProperty("$ref", out var reference) &&
        !Check(value, Reference(reference, root), root, depth + 1, ref remainingWork, output)) return false;
    if (schema.TryGetProperty("type", out var type) && !MatchesType(value, type, output)) return false;
    if (!CheckOutputAssertions(value, schema, output, ref remainingWork)) return false;
    foreach (var keyword in new[] { "allOf", "anyOf", "oneOf" })
    {
      if (skipRootAnyOf && depth == 0 && keyword == "anyOf") continue;
      if (!schema.TryGetProperty(keyword, out var branches)) continue;
      var matches = 0;
      foreach (var branch in branches.EnumerateArray())
        if (Check(value, branch, root, depth + 1, ref remainingWork, output)) matches++;
      if (keyword == "allOf" && matches != branches.GetArrayLength() ||
          keyword == "anyOf" && matches == 0 || keyword == "oneOf" && matches != 1) return false;
    }
    return value.ValueKind switch
    {
      JsonValueKind.Object => CheckObject(value, schema, root, depth, ref remainingWork, output),
      JsonValueKind.Array => CheckArray(value, schema, root, depth, ref remainingWork, output),
      JsonValueKind.String => CheckString(value.GetString()!, schema),
      JsonValueKind.Number => CheckNumber(value, schema, output, ref remainingWork),
      _ => true,
    };
  }

  private static bool MatchesType(JsonElement value, JsonElement type, bool output)
  {
    if (type.ValueKind == JsonValueKind.Array)
      return type.EnumerateArray().Any(candidate => MatchesType(value, candidate, output));
    return type.GetString() switch
    {
      "object" => value.ValueKind == JsonValueKind.Object,
      "array" => value.ValueKind == JsonValueKind.Array,
      "string" => value.ValueKind == JsonValueKind.String,
      "integer" => output
          ? value.ValueKind == JsonValueKind.Number && CanonicalNumber(value.GetRawText(), out _, out _, out var exponent) && exponent >= 0
          : Number(value, out var number) && decimal.Truncate(number) == number,
      "number" => value.ValueKind == JsonValueKind.Number,
      "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
      "null" => value.ValueKind == JsonValueKind.Null,
      _ => false,
    };
  }

  private static bool CheckObject(JsonElement value, JsonElement schema, JsonElement root, int depth, ref int remainingWork, bool output)
  {
    var fields = value.EnumerateObject().ToArray();
    if (fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length)
      return false;
    if (!Within(fields.Length, schema, "minProperties", "maxProperties")) return false;
    if (schema.TryGetProperty("required", out var required) &&
        !required.EnumerateArray().All(name => value.TryGetProperty(name.GetString()!, out _))) return false;
    var hasProperties = schema.TryGetProperty("properties", out var properties);
    var hasAdditional = schema.TryGetProperty("additionalProperties", out var additional);
    foreach (var field in fields)
    {
      if (hasProperties && properties.TryGetProperty(field.Name, out var declared))
      {
        if (!Check(field.Value, declared, root, depth + 1, ref remainingWork, output)) return false;
      }
      else if (hasAdditional)
      {
        if (additional.ValueKind == JsonValueKind.False ||
            additional.ValueKind == JsonValueKind.Object &&
            !Check(field.Value, additional, root, depth + 1, ref remainingWork, output)) return false;
      }
    }
    return true;
  }

  private static bool CheckArray(JsonElement value, JsonElement schema, JsonElement root, int depth, ref int remainingWork, bool output)
  {
    var items = value.EnumerateArray().ToArray();
    if (!Within(items.Length, schema, "minItems", "maxItems")) return false;
    if (schema.TryGetProperty("uniqueItems", out var unique) && unique.ValueKind == JsonValueKind.True)
    {
      if (items.Length > 256) return false;
      for (var i = 0; i < items.Length; i++)
        for (var j = i + 1; j < items.Length; j++)
          if (output ? SameOutput(items[i], items[j], ref remainingWork) : Same(items[i], items[j])) return false;
    }
    if (!schema.TryGetProperty("items", out var itemSchema)) return true;
    foreach (var item in items)
      if (!Check(item, itemSchema, root, depth + 1, ref remainingWork, output)) return false;
    return true;
  }

  private static bool CheckString(string text, JsonElement schema)
  {
    if (!Within(text.EnumerateRunes().Count(), schema, "minLength", "maxLength")) return false;
    if (schema.TryGetProperty("format", out var format))
    {
      if (format.GetString() == "date-time" && !ValidDateTime(text)) return false;
      if (format.GetString() == "uuid" &&
          (text.Length != 36 || !Guid.TryParseExact(text, "D", out _))) return false;
      if (format.GetString() == "uri" && !StrictUri(text)) return false;
    }
    if (schema.TryGetProperty("pattern", out var pattern))
    {
      try { if (!BoundedPattern(pattern.GetString()!).IsMatch(text)) return false; }
      catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { return false; }
    }
    return true;
  }

  private static bool CheckNumber(JsonElement value, JsonElement schema, bool output, ref int remainingWork)
  {
    if (output) return CheckOutputNumber(value, schema, ref remainingWork);
    if (!Number(value, out var number)) return false;
    if (schema.TryGetProperty("minimum", out var minimum) &&
        (!Number(minimum, out var lower) || number < lower))
      return false;
    if (schema.TryGetProperty("maximum", out var maximum) &&
        (!Number(maximum, out var upper) || number > upper))
      return false;
    return true;
  }

  private static bool StrictUri(string text)
  {
    const string punctuation = "-._~:/?#[]@!$&'()*+,;=%";
    for (var index = 0; index < text.Length; index++)
    {
      var character = text[index];
      if (!char.IsAsciiLetterOrDigit(character) &&
          !punctuation.Contains(character, StringComparison.Ordinal)) return false;
      if (character != '%') continue;
      if (index + 2 >= text.Length || !Uri.IsHexDigit(text[index + 1]) ||
          !Uri.IsHexDigit(text[index + 2])) return false;
      index += 2;
    }
    return Uri.TryCreate(text, UriKind.Absolute, out _);
  }

  private static bool Within(int count, JsonElement schema, string minimum, string maximum) =>
      (!schema.TryGetProperty(minimum, out var min) || Count(min, out var lower) && count >= lower) &&
      (!schema.TryGetProperty(maximum, out var max) || Count(max, out var upper) && count <= upper);
}

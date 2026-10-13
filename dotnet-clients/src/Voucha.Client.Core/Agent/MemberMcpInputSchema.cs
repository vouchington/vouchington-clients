using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>A bounded, fail-closed JSON Schema subset for server-advertised MCP inputs.</summary>
internal static partial class MemberMcpInputSchema
{
  private const int MaxDepth = 32;
  private static readonly HashSet<string> Types = ["object", "array", "string", "integer", "number", "boolean", "null"];
  private static readonly HashSet<string> Annotations =
      ["title", "description", "default", "examples", "deprecated", "readOnly", "writeOnly"];

  public static void Inspect(JsonElement schema)
  {
    InspectNode(schema, schema, 0);
  }

  public static bool Accepts(JsonElement value, JsonElement schema)
  {
    Inspect(schema);
    var remainingWork = 4_096;
    return Check(value, schema, schema, 0, ref remainingWork);
  }

  private static void InspectNode(JsonElement schema, JsonElement root, int depth)
  {
    if (depth >= MaxDepth || schema.ValueKind != JsonValueKind.Object)
      throw new InvalidDataException("Unsupported MCP input schema.");
    if (schema.TryGetProperty("uniqueItems", out var unique) && unique.ValueKind == JsonValueKind.True &&
        (!schema.TryGetProperty("maxItems", out var maximum) || !Count(maximum, out var count) || count > 256))
      throw new InvalidDataException("Unbounded MCP uniqueItems schema.");
    foreach (var field in schema.EnumerateObject())
    {
      var item = field.Value;
      switch (field.Name)
      {
        case "type":
          if (item.ValueKind == JsonValueKind.String && Types.Contains(item.GetString()!)) break;
          if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0 &&
              item.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String &&
                  Types.Contains(value.GetString()!))) break;
          throw new InvalidDataException("Unsupported MCP schema type.");
        case "$ref":
          _ = Reference(item, root);
          break;
        case "properties":
        case "$defs":
          if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid MCP schema map.");
          foreach (var child in item.EnumerateObject()) InspectNode(child.Value, root, depth + 1);
          break;
        case "items":
          InspectNode(item, root, depth + 1);
          break;
        case "additionalProperties":
          if (item.ValueKind is JsonValueKind.True or JsonValueKind.False) break;
          InspectNode(item, root, depth + 1);
          break;
        case "oneOf":
        case "anyOf":
        case "allOf":
          if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() == 0)
            throw new InvalidDataException("Invalid MCP schema composition.");
          foreach (var child in item.EnumerateArray()) InspectNode(child, root, depth + 1);
          break;
        case "required":
          if (item.ValueKind != JsonValueKind.Array ||
              !item.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String))
            throw new InvalidDataException("Invalid MCP schema required list.");
          break;
        case "enum":
          if (item.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid MCP schema enum.");
          break;
        case "uniqueItems":
          if (item.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Invalid MCP schema uniqueItems.");
          break;
        case "minimum":
        case "maximum":
          if (!Number(item, out _)) throw new InvalidDataException("Unsupported MCP numeric bound.");
          break;
        case "minLength":
        case "maxLength":
        case "minItems":
        case "maxItems":
        case "minProperties":
        case "maxProperties":
          if (!Count(item, out _)) throw new InvalidDataException("Unsupported MCP count bound.");
          break;
        case "format":
          if (item.ValueKind != JsonValueKind.String || item.GetString() is not ("uuid" or "uri"))
            throw new InvalidDataException("Unsupported MCP string format.");
          break;
        case "pattern":
          if (item.ValueKind != JsonValueKind.String) throw new InvalidDataException("Invalid MCP pattern.");
          try { _ = BoundedPattern(item.GetString()!); }
          catch (Exception error) when (error is ArgumentException or NotSupportedException)
          {
            throw new InvalidDataException("Unsupported MCP pattern.", error);
          }
          break;
        case "const":
          break;
        default:
          if (!Annotations.Contains(field.Name)) throw new InvalidDataException("Unsupported MCP schema keyword.");
          break;
      }
    }
  }

  private static JsonElement Reference(JsonElement item, JsonElement root)
  {
    if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } path ||
        !path.StartsWith("#/$defs/", StringComparison.Ordinal) || path.Length <= 8 ||
        path.AsSpan(8).Contains('/')) throw new InvalidDataException("Unsupported MCP schema reference.");
    var encoded = path.AsSpan(8);
    var name = new System.Text.StringBuilder(encoded.Length);
    for (var index = 0; index < encoded.Length; index++)
    {
      if (encoded[index] != '~') { name.Append(encoded[index]); continue; }
      if (++index == encoded.Length) throw new InvalidDataException("Invalid MCP schema reference escape.");
      name.Append(encoded[index] switch
      {
        '0' => '~',
        '1' => '/',
        _ => throw new InvalidDataException("Invalid MCP schema reference escape."),
      });
    }
    if (!root.TryGetProperty("$defs", out var defs) || defs.ValueKind != JsonValueKind.Object ||
        !defs.TryGetProperty(name.ToString(), out var target))
      throw new InvalidDataException("Missing MCP schema definition.");
    return target;
  }
}

using System.Globalization;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

internal sealed class MemberMcpOutputContract
{
  private readonly JsonElement root;
  private readonly Dictionary<string, JsonElement>? options;

  public MemberMcpOutputContract(JsonElement tool)
  {
    root = tool.GetProperty("outputSchema").Clone();
    MemberMcpInputSchema.Inspect(root, output: true);
    var names = OptionNames(tool.GetProperty("inputSchema"));
    var hasMapping = tool.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object &&
        meta.TryGetProperty("voucha/optionOutputPointers", out _);
    if (!hasMapping)
    {
      if (names is not null) throw new InvalidDataException("Missing MCP option output mapping.");
      if (tool.TryGetProperty("_meta", out meta) && meta.ValueKind != JsonValueKind.Object)
        throw new InvalidDataException("Invalid MCP tool metadata.");
      return;
    }
    var mapping = meta.GetProperty("voucha/optionOutputPointers");
    if (mapping.ValueKind != JsonValueKind.Object || names is null)
      throw new InvalidDataException("Invalid MCP option output mapping.");
    options = new(StringComparer.Ordinal);
    foreach (var entry in mapping.EnumerateObject())
    {
      if (entry.Value.ValueKind != JsonValueKind.String ||
          !options.TryAdd(entry.Name, Resolve(entry.Value.GetString()!)))
        throw new InvalidDataException("Invalid MCP option output pointer.");
    }
    if (names.Count == 0 || !names.SetEquals(options.Keys))
      throw new InvalidDataException("Incomplete MCP option output mapping.");
  }

  public void Validate(McpToolResult result, JsonElement arguments)
  {
    if (result.IsError == true) return;
    var selected = root;
    if (options is not null)
    {
      if (!arguments.TryGetProperty("option", out var option) || option.ValueKind != JsonValueKind.String ||
          !options.TryGetValue(option.GetString()!, out selected))
        throw new InvalidDataException("Missing selected MCP output contract.");
    }
    if (result.StructuredContent is not { } value ||
        !MemberMcpInputSchema.AcceptsOutput(value, selected, root))
      throw new InvalidDataException("Invalid MCP structured output.");
  }

  internal static HashSet<string>? OptionNames(JsonElement schema)
  {
    if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("oneOf", out var branches) ||
        branches.ValueKind != JsonValueKind.Array || branches.GetArrayLength() == 0) return null;
    var names = new HashSet<string>(StringComparer.Ordinal);
    foreach (var branch in branches.EnumerateArray())
    {
      if (branch.ValueKind != JsonValueKind.Object ||
          !branch.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "object" ||
          !branch.TryGetProperty("additionalProperties", out var closed) || closed.ValueKind != JsonValueKind.False ||
          !branch.TryGetProperty("required", out var required) || required.ValueKind != JsonValueKind.Array ||
          required.GetArrayLength() != 2 || !required.EnumerateArray().Any(item => item.GetString() == "option") ||
          !required.EnumerateArray().Any(item => item.GetString() == "arguments") ||
          !branch.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object ||
          properties.EnumerateObject().Count() != 2 || !properties.TryGetProperty("arguments", out _) ||
          !properties.TryGetProperty("option", out var option) || option.ValueKind != JsonValueKind.Object)
        return null;
      if (option.TryGetProperty("const", out var literal) && literal.ValueKind == JsonValueKind.String)
        names.Add(literal.GetString()!);
      else if (option.TryGetProperty("enum", out var choices) && choices.ValueKind == JsonValueKind.Array &&
          choices.GetArrayLength() == 1 && choices[0].ValueKind == JsonValueKind.String)
        names.Add(choices[0].GetString()!);
      else return null;
    }
    if (names.Count != branches.GetArrayLength())
      throw new InvalidDataException("Duplicate MCP option literal.");
    return names;
  }

  private JsonElement Resolve(string pointer)
  {
    if (pointer == "#") return root;
    var parts = pointer.Split('/');
    if (parts.Length != 3 || parts[0] != "#" || parts[1] != "anyOf" ||
        !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
        index < 0 || index.ToString(CultureInfo.InvariantCulture) != parts[2] ||
        !root.TryGetProperty("anyOf", out var branches) || branches.ValueKind != JsonValueKind.Array ||
        index >= branches.GetArrayLength())
      throw new InvalidDataException("Invalid local MCP output pointer.");
    return branches[index];
  }
}

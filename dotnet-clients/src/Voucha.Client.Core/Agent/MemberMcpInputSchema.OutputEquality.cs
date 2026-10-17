using System.Text.Json;
using System.Text;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  private static bool CheckOutputAssertions(JsonElement value, JsonElement schema, bool output, ref int remainingWork)
  {
    if (schema.TryGetProperty("const", out var constant) &&
        !(output ? SameOutput(value, constant, ref remainingWork) : Same(value, constant))) return false;
    if (!schema.TryGetProperty("enum", out var choices)) return true;
    foreach (var choice in choices.EnumerateArray())
      if (output ? SameOutput(value, choice, ref remainingWork) : Same(value, choice)) return true;
    return false;
  }

  private static bool SameOutput(JsonElement left, JsonElement right, ref int remainingWork)
  {
    if (remainingWork-- <= 0) throw new InvalidDataException("MCP schema validation work exceeded.");
    if (left.ValueKind != right.ValueKind) return false;
    if (left.ValueKind == JsonValueKind.Number)
      return CompareOutputNumbers(left, right, ref remainingWork, out var comparison) && comparison == 0;
    if (left.ValueKind == JsonValueKind.Array)
    {
      if (left.GetArrayLength() != right.GetArrayLength()) return false;
      for (var index = 0; index < left.GetArrayLength(); index++)
        if (!SameOutput(left[index], right[index], ref remainingWork)) return false;
      return true;
    }
    if (left.ValueKind == JsonValueKind.Object)
      return SameOutputObjects(left, right, ref remainingWork);
    if (left.ValueKind == JsonValueKind.String)
    {
      var first = left.GetString()!;
      var second = right.GetString()!;
      ChargeOutputText(first, ref remainingWork);
      ChargeOutputText(second, ref remainingWork);
      return string.Equals(first, second, StringComparison.Ordinal);
    }
    return Same(left, right);
  }
  private static void ChargeOutputText(string text, ref int remainingWork)
  {
    var bytes = Encoding.UTF8.GetByteCount(text);
    if (bytes > remainingWork) throw new InvalidDataException("MCP schema validation work exceeded.");
    remainingWork -= bytes;
  }

  private static bool SameOutputObjects(JsonElement left, JsonElement right, ref int remainingWork)
  {
    var expected = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    foreach (var field in right.EnumerateObject())
    {
      ChargeOutputText(field.Name, ref remainingWork);
      if (!expected.TryAdd(field.Name, field.Value)) return false;
    }
    var actualCount = 0;
    foreach (var field in left.EnumerateObject())
    {
      ChargeOutputText(field.Name, ref remainingWork);
      actualCount++;
      if (!expected.TryGetValue(field.Name, out var other) ||
          !SameOutput(field.Value, other, ref remainingWork)) return false;
    }
    return actualCount == expected.Count;
  }

}

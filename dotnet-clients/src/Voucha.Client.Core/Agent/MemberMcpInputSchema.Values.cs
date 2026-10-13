using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  private static bool Number(JsonElement value, out decimal number)
  {
    number = 0;
    return value.ValueKind == JsonValueKind.Number &&
        decimal.TryParse(value.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
  }

  private static bool Count(JsonElement value, out int count)
  {
    count = 0;
    return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out count) && count >= 0;
  }

  private static bool Same(JsonElement left, JsonElement right)
  {
    if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
      return Number(left, out var a) && Number(right, out var b) && a == b;
    if (left.ValueKind != right.ValueKind) return false;
    return left.ValueKind switch
    {
      JsonValueKind.Object => SameObject(left, right),
      JsonValueKind.Array => left.GetArrayLength() == right.GetArrayLength() &&
          left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => Same(pair.First, pair.Second)),
      JsonValueKind.String => left.GetString() == right.GetString(),
      JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
      _ => false,
    };
  }

  private static bool SameObject(JsonElement left, JsonElement right)
  {
    if (left.EnumerateObject().Count() != right.EnumerateObject().Count()) return false;
    return left.EnumerateObject().All(field =>
        right.TryGetProperty(field.Name, out var value) && Same(field.Value, value));
  }

  private static Regex BoundedPattern(string pattern)
  {
    if (pattern.Length > 4096) throw new ArgumentException("MCP pattern too long.", nameof(pattern));
    return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));
  }
}

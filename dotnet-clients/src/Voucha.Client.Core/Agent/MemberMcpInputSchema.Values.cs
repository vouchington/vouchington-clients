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
        decimal.TryParse(value.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
        SameExactDecimal(value.GetRawText(), number);
  }

  // Decimal.TryParse can silently round or underflow; constrained MCP numbers must compare exactly.
  private static bool SameExactDecimal(string raw, decimal number) =>
      CanonicalNumber(raw, out var rawSign, out var rawDigits, out var rawExponent) &&
      CanonicalNumber(number.ToString("G29", CultureInfo.InvariantCulture),
          out var parsedSign, out var parsedDigits, out var parsedExponent) &&
      rawSign == parsedSign && rawDigits == parsedDigits && rawExponent == parsedExponent;

  private static bool CanonicalNumber(
      string raw, out bool negative, out string digits, out long exponent)
  {
    negative = raw.StartsWith('-');
    if (negative) raw = raw[1..];
    exponent = 0;
    var exponentIndex = raw.IndexOfAny(['e', 'E']);
    if (exponentIndex >= 0)
    {
      if (!long.TryParse(raw.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign,
              CultureInfo.InvariantCulture, out exponent))
      {
        digits = string.Empty;
        return false;
      }
      raw = raw[..exponentIndex];
    }
    var decimalIndex = raw.IndexOf('.', StringComparison.Ordinal);
    var fractionalDigits = decimalIndex < 0 ? 0 : raw.Length - decimalIndex - 1;
    digits = raw.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
    if (digits.Length == 0)
    {
      negative = false;
      exponent = 0;
      digits = "0";
      return true;
    }
    var trailingZeros = digits.Length - digits.TrimEnd('0').Length;
    digits = digits.TrimEnd('0');
    try { exponent = checked(exponent - fractionalDigits + trailingZeros); }
    catch (OverflowException) { return false; }
    return true;
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

using System.Globalization;
using System.Text;
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
    {
      if (!CanonicalNumber(left.GetRawText(), out var leftSign, out var leftDigits, out var leftExponent) ||
          !CanonicalNumber(right.GetRawText(), out var rightSign, out var rightDigits, out var rightExponent))
        throw new InvalidDataException("Unsupported MCP numeric equality.");
      return leftSign == rightSign && leftDigits == rightDigits && leftExponent == rightExponent;
    }
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
    return new Regex(EcmaPattern(pattern), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));
  }

  private const string EcmaWhitespace = @"\u0009-\u000D\u0020\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";

  private static string EcmaPattern(string pattern)
  {
    var result = new StringBuilder(pattern.Length);
    var inClass = false;
    for (var index = 0; index < pattern.Length; index++)
    {
      var character = pattern[index];
      if (character == '\\')
      {
        if (++index == pattern.Length) throw new ArgumentException("Invalid MCP pattern escape.", nameof(pattern));
        character = pattern[index];
        if (character is 'd' or 'D' or 'w' or 'W' or 's' or 'S')
        {
          if (inClass) throw new ArgumentException("Unsupported MCP character class escape.", nameof(pattern));
          result.Append(character switch
          {
            'd' => "[0-9]",
            'D' => "[^0-9]",
            'w' => "[A-Za-z0-9_]",
            'W' => "[^A-Za-z0-9_]",
            's' => $"[{EcmaWhitespace}]",
            _ => $"[^{EcmaWhitespace}]",
          });
        }
        else
        {
          if (character is not ('n' or 'r' or 't' or 'f' or 'v' or '\\' or '/' or '.' or '^' or '$'
              or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '|' or '-'))
            throw new ArgumentException("Unsupported MCP pattern escape.", nameof(pattern));
          result.Append('\\').Append(character);
        }
        continue;
      }
      if (character == '[')
      {
        if (inClass) throw new ArgumentException("Unsupported MCP nested character class.", nameof(pattern));
        inClass = true;
      }
      else if (character == ']') inClass = false;
      if (character == '(' && !inClass && index + 1 < pattern.Length && pattern[index + 1] == '?' &&
          (index + 2 >= pattern.Length || pattern[index + 2] != ':'))
        throw new ArgumentException("Unsupported MCP group extension.", nameof(pattern));
      if (character == '$' && !inClass)
      {
        result.Append(@"\z");
        continue;
      }
      result.Append(character == '.' && !inClass ? @"[^\n\r\u2028\u2029]" : character.ToString());
    }
    return result.ToString();
  }
}

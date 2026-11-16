using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  public static bool AcceptsOutput(JsonElement value, JsonElement schema, JsonElement root, int? selectedRootAnyOfIndex = null)
  {
    var remainingWork = 262_144;
    if (!HasUniqueObjectMembers(value, 0, ref remainingWork)) return false;
    remainingWork = 262_144;
    if (selectedRootAnyOfIndex is { } index)
      return CheckSelectedRootOutput(value, root, index, ref remainingWork);
    return Check(value, root, root, 0, ref remainingWork, output: true) &&
        (schema.Equals(root) || Check(value, schema, root, 0, ref remainingWork, output: true));
  }

  private static bool CheckOutputNumber(JsonElement value, JsonElement schema, ref int remainingWork)
  {
    foreach (var keyword in new[] { "minimum", "maximum" })
    {
      if (!schema.TryGetProperty(keyword, out var bound)) continue;
      if (!CompareOutputNumbers(value, bound, ref remainingWork, out var comparison) ||
          keyword == "minimum" && comparison < 0 || keyword == "maximum" && comparison > 0)
        return false;
    }
    return true;
  }

  private static bool CompareOutputNumbers(JsonElement left, JsonElement right, ref int remainingWork, out int comparison)
  {
    comparison = 0;
    if (left.ValueKind != JsonValueKind.Number || right.ValueKind != JsonValueKind.Number) return false;
    var leftRaw = left.GetRawText();
    var rightRaw = right.GetRawText();
    if (leftRaw.Length > remainingWork || rightRaw.Length > remainingWork - leftRaw.Length)
      throw new InvalidDataException("MCP schema validation work exceeded.");
    remainingWork -= leftRaw.Length + rightRaw.Length;
    if (!CanonicalNumber(leftRaw, out var leftNegative, out var leftDigits, out var leftExponent) ||
        !CanonicalNumber(rightRaw, out var rightNegative, out var rightDigits, out var rightExponent)) return false;
    if (leftDigits == "0" || rightDigits == "0")
    {
      comparison = leftDigits == rightDigits ? 0 : leftDigits == "0"
          ? rightNegative ? 1 : -1 : leftNegative ? -1 : 1;
      return true;
    }
    if (leftNegative != rightNegative)
    {
      comparison = leftNegative ? -1 : 1;
      return true;
    }
    comparison = (new BigInteger(leftExponent) + leftDigits.Length)
        .CompareTo(new BigInteger(rightExponent) + rightDigits.Length);
    if (comparison == 0)
      for (var index = 0; index < Math.Max(leftDigits.Length, rightDigits.Length); index++)
      {
        comparison = (index < leftDigits.Length ? leftDigits[index] : '0')
            .CompareTo(index < rightDigits.Length ? rightDigits[index] : '0');
        if (comparison != 0) break;
      }
    if (leftNegative) comparison = -comparison;
    return true;
  }

  // A linear scan instead of a regex: the format is fixed, so there is no match deadline to miss under load.
  private static bool ValidDateTime(string text)
  {
    var at = 0;
    bool Digits(int count, out int number)
    {
      number = 0;
      if (text.Length - at < count) return false;
      for (var i = 0; i < count; i++)
      {
        var digit = text[at + i] - '0';
        if (digit is < 0 or > 9) return false;
        number = number * 10 + digit;
      }
      at += count;
      return true;
    }
    bool Literal(char expected) => at < text.Length && text[at] == expected && ++at > 0;
    if (!Digits(4, out var year) || !Literal('-') || !Digits(2, out var month) || !Literal('-') ||
        !Digits(2, out var day)) return false;
    if (at == text.Length || !(text[at] is 'T' or 't' || DateTimeSeparators.Contains(text[at], StringComparison.Ordinal))) return false;
    at++;
    if (!Digits(2, out var hour) || !Literal(':') || !Digits(2, out var minute) || !Literal(':') ||
        !Digits(2, out var second)) return false;
    if (Literal('.'))
    {
      var start = at;
      while (at < text.Length && text[at] is >= '0' and <= '9') at++;
      if (at == start) return false;
    }
    var sign = 1;
    int offsetHour = 0, offsetMinute = 0;
    if (!(Literal('Z') || Literal('z')))
    {
      if (Literal('-')) sign = -1;
      else if (!Literal('+')) return false;
      if (!Digits(2, out offsetHour)) return false;
      Literal(':');
      if (at < text.Length && !Digits(2, out offsetMinute)) return false;
    }
    if (at != text.Length) return false;
    var leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    int[] days = [0, 31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    if (month is < 1 or > 12 || day < 1 || day > days[month]) return false;
    if (hour > 23 || minute > 59 || offsetHour > 23 || offsetMinute > 59 || second > 60) return false;
    if (second < 60) return true;
    var utcMinute = hour * 60 + minute - (offsetHour * 60 + offsetMinute) * sign;
    return (utcMinute == 1439 && day == days[month]) || (utcMinute == -1 && day == 1);
  }

  private const string DateTimeSeparators =
      "\t\n\v\f\r \u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";
}

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  private static readonly Regex DateTimePattern = new(
      @"^([0-9]{4})-([0-9]{2})-([0-9]{2})[Tt\x09-\x0D\x20\xA0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF]([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.[0-9]+)?" +
      @"(?:([Zz])|([+-])([0-9]{2})(?::?([0-9]{2}))?)\z",
      RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

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

  private static bool ValidDateTime(string text)
  {
    var match = DateTimePattern.Match(text);
    if (!match.Success) return false;
    int Part(int index) => int.TryParse(match.Groups[index].Value, NumberStyles.None,
        CultureInfo.InvariantCulture, out var value) ? value : 0;
    var year = Part(1);
    var month = Part(2);
    var day = Part(3);
    var leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    int[] days = [0, 31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    if (month is < 1 or > 12 || day < 1 || day > days[month]) return false;
    var hour = Part(4);
    var minute = Part(5);
    var second = Part(6);
    var offsetHour = Part(9);
    var offsetMinute = Part(10);
    if (hour > 23 || minute > 59 || offsetHour > 23 || offsetMinute > 59 || second > 60) return false;
    if (second < 60) return true;
    var sign = match.Groups[8].Value == "-" ? -1 : 1;
    var utcMinute = minute - offsetMinute * sign;
    var utcHour = hour - offsetHour * sign - (utcMinute < 0 ? 1 : 0);
    return (utcHour is 23 or -1) && (utcMinute is 59 or -1);
  }
}

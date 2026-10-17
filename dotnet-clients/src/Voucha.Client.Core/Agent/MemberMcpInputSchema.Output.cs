using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  private static readonly Regex DateTimePattern = new(
      @"^([0-9]{4})-([0-9]{2})-([0-9]{2})[Tt\s]([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.[0-9]+)?" +
      @"(?:([Zz])|([+-])([0-9]{2})(?::?([0-9]{2}))?)\z",
      RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

  public static bool AcceptsOutput(JsonElement value, JsonElement schema, JsonElement root)
  {
    var remainingWork = 262_144;
    if (!HasUniqueObjectMembers(value, 0, ref remainingWork)) return false;
    remainingWork = 262_144;
    return Check(value, schema, root, 0, ref remainingWork);
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

namespace Voucha.Client.Core.Auth;

internal static class NativeCallbackQuery
{
  public static Dictionary<string, string> Parse(string query) =>
      query.TrimStart('?')
          .Split('&', StringSplitOptions.RemoveEmptyEntries)
          .Select(item => item.Split('=', 2))
          .Where(parts => parts.Length == 2)
          .GroupBy(parts => Uri.UnescapeDataString(parts[0]), StringComparer.Ordinal)
          .ToDictionary(
              group => group.Key,
              group => Uri.UnescapeDataString(group.First()[1]),
              StringComparer.Ordinal);
}

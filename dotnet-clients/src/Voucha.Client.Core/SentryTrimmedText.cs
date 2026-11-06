namespace Voucha.Client.Core;

internal static class SentryTrimmedText
{
  internal static string? NonEmpty(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrEmpty(trimmed) ? null : trimmed;
  }
}

namespace Voucha.Client.Core.Support;

public static class TrimmedText
{
  public static string? NonEmpty(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrEmpty(trimmed) ? null : trimmed;
  }
}

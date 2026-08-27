namespace Voucha.Client.Core.Moderation;

public static class ModerationTransparencyRange
{
  public const string Today = "today";
  public const string SevenDays = "7d";
  public const string Default = "30d";
  public const string NinetyDays = "90d";
  public const string All = "all";

  public static IReadOnlyList<string> AllValues { get; } = [Today, SevenDays, Default, NinetyDays, All];

  public static string ParseOrDefault(string? value) =>
      value is not null && AllValues.Contains(value, StringComparer.Ordinal) ? value : Default;
}

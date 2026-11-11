namespace Voucha.Client.Core.Posts;

/// <summary>
/// Finite <c>time_range</c> values for post lists. The default selection is <c>null</c>, which
/// omits the parameter so the server applies its across-age bounded window.
/// </summary>
public static class PostTimeRanges
{
  public const string LastDay = "1d";
  public const string LastWeek = "1w";
  public const string Last30Days = "1m";
  public const string LastYear = "1y";
  public const string AllTime = "all";
}

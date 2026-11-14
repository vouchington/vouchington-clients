namespace Voucha.Client.Core.Posts;

/// <summary>
/// Finite <c>time_range</c> values for post lists. Browse lists send <c>1m</c> explicitly
/// because omitting the parameter leaves the server query unbounded.
/// </summary>
public static class PostTimeRanges
{
  public const string LastDay = "1d";
  public const string LastWeek = "1w";
  public const string Last30Days = "1m";
  public const string LastYear = "1y";
  public const string AllTime = "all";
}

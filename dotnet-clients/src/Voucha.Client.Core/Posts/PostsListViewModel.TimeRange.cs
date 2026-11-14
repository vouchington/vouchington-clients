namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel
{
  public static IReadOnlyList<string> HackerNewsTimeRangeOptions { get; } =
      [PostTimeRanges.LastWeek, PostTimeRanges.Last30Days, PostTimeRanges.LastYear];

  public async Task SelectBrowseTimeRangeAsync(string value, CancellationToken cancellationToken = default)
  {
    if (!TimeRange.Select(value)) return;
    await LoadBrowseAsync(activePostTypes, TimeRange.RequestValue,
        hasHackerNewsReferences: HackerNewsFilter,
        cancellationToken: cancellationToken, scope: activeBrowseRequest).ConfigureAwait(true);
  }

  private string ConfigureBrowseTimeRange(string? requested, bool? hackerNews)
  {
    var range = hackerNews == true && requested is null or PostTimeRanges.AllTime
        ? PostTimeRanges.LastYear : requested ?? PostTimeRanges.Last30Days;
    TimeRange.Reconfigure(hackerNews == true ? HackerNewsTimeRangeOptions : PostTimeRangeSelection.BrowseOptions,
        range, preserveSelection: false);
    return range;
  }

  // The config rejected a requested filter: restore the ordinary options (including all time) the
  // filtered request had narrowed, keeping the selected range.
  private string ReconcileBrowseTimeRange(string range, bool? requested, bool? effective)
  {
    if (requested != true || effective == true) return range;
    return activeTimeRange = ConfigureBrowseTimeRange(range, null);
  }
}

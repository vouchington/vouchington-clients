using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private readonly EmptyPageContinuation newsEmptyPages = new();
  private bool communityNewsHasNextPage;

  /// <summary>
  /// Whether the selected list continues. News follows the server's <c>has_next_page</c>; the other
  /// sections still continue while they hold a cursor.
  /// </summary>
  private bool HasCommunityListContinuation =>
      communityListEndCursor is not null &&
      (SelectedSection != CommunityDetailSurfaceSection.News || communityNewsHasNextPage);

  private Task LoadInitialNewsAsync(CancellationToken cancellationToken)
  {
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityListRevision);
    return LoadNewsPagesAsync(
        community,
        null,
        append: false,
        () => revision == Volatile.Read(ref communityListRevision) &&
            SelectedSection == CommunityDetailSurfaceSection.News &&
            string.Equals(communityIdOrSlug, community, StringComparison.Ordinal),
        cancellationToken);
  }

  /// <summary>
  /// Loads a news page and keeps following <c>has_next_page: true</c> through pages that add no rows,
  /// up to <see cref="EmptyPageContinuation.MaxConsecutiveEmptyPages"/>. After that the manual
  /// "Load more" control continues because <see cref="HasMoreCommunityList"/> stays true.
  /// </summary>
  private async Task LoadNewsPagesAsync(
      string community,
      string? after,
      bool append,
      Func<bool> isCurrent,
      CancellationToken cancellationToken)
  {
    newsEmptyPages.Restart();
    while (true)
    {
      var page = await service.FetchNewsPageAsync(community, after, 25, cancellationToken).ConfigureAwait(true);
      if (!isCurrent()) return;
      var rows = page.Results
          .Select(reference => NewsRow(reference, page.RssFeedItems, page.RssFeedItemEmbeds))
          .Where(row => row is not null)
          .Select(row => row!);
      var previousCount = append ? News.Count : 0;
      News = append ? AppendUnique(News, rows, row => row.Id) : rows.ToArray();
      communityListEndCursor = page.PageInfo.EndCursor;
      communityNewsHasNextPage = page.PageInfo.HasNextPage;
      OnPropertyChanged(nameof(CanLoadMoreCommunityList));
      OnPropertyChanged(nameof(HasMoreCommunityList));
      newsEmptyPages.Record(News.Count > previousCount);
      if (!newsEmptyPages.ShouldContinue(HasCommunityListContinuation)) return;
      after = communityListEndCursor;
      append = true;
    }
  }
}

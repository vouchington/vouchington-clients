namespace Voucha.Client.Core.Pagination;

/// <summary>
/// Bounds how many consecutive pages that add no rows a list follows on its own while the server
/// keeps answering <c>has_next_page: true</c>. Past the cap the list stops and the user continues
/// with the manual "Load more" control, so a server that only returns empty pages cannot loop a client.
/// </summary>
public sealed class EmptyPageContinuation
{
  public const int MaxConsecutiveEmptyPages = 5;

  public int ConsecutiveEmptyPages { get; private set; }

  /// <summary>Re-arms the budget for a user- or view-initiated page.</summary>
  public void Restart() => ConsecutiveEmptyPages = 0;

  public void Record(bool addedRows) => ConsecutiveEmptyPages = addedRows ? 0 : ConsecutiveEmptyPages + 1;

  public bool ShouldContinue(bool hasMore) =>
      hasMore && ConsecutiveEmptyPages > 0 && ConsecutiveEmptyPages < MaxConsecutiveEmptyPages;

  public bool NeedsManualContinuation(bool hasMore) =>
      hasMore && ConsecutiveEmptyPages >= MaxConsecutiveEmptyPages;
}

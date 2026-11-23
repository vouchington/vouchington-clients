using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  private readonly CursorPaginationState<NewsFeedItem, string> feedPages = new(item => item.FeedRowId);

  public bool HasMore => feedPages.HasLoadedPage && feedPages.HasMore;
  public bool IsLoadingMore => feedPages.IsLoading && !IsLoading;
  public bool HasPaginationError => !string.IsNullOrWhiteSpace(PaginationErrorMessage);
  public string? PaginationErrorMessage => feedPages.LastError;

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      LoadFeedPageAsync(replace: false, cancellationToken);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "The feed UI converts continuation failures into retry state instead of surfacing them from async event handlers.")]
  private async Task LoadFeedPageAsync(
      bool replace,
      CancellationToken cancellationToken,
      Task? hackerNewsConfig = null)
  {
    var requestId = replace ? Interlocked.Increment(ref loadRequestId) : Volatile.Read(ref loadRequestId);
    var scope = SelectedScope;
    var sourceFeedType = SelectedSourceFeedType;
    if (replace) feedPages.Reset();
    else feedPages.ReplaceItems(Items);
    var request = replace ? feedPages.BeginInitialPageIfNeeded() : feedPages.BeginNextPage();
    if (request is null) return;

    if (replace)
    {
      IsLoading = true;
      ErrorMessage = null;
    }
    NotifyPaginationChanged();

    var current = request;
    var firstPage = replace;
    bool? hackerNewsReferences = null;
    try
    {
      // Follows has_next_page through pages that add no rows (bounded by EmptyPageContinuation), so an
      // empty or fully filtered page does not strand the feed on its empty state.
      while (true)
      {
        // A requested filter is only sent once the flag is known on; an unfiltered load never waits for it.
        if (firstPage && hackerNewsConfig is not null && hasHackerNewsReferences is not null) await hackerNewsConfig.ConfigureAwait(true);
        // Resolved once so a flag flip mid-continuation cannot pair a filtered cursor with an unfiltered query.
        hackerNewsReferences ??= EffectiveHackerNewsReferences;
        var page = await LoadNewsFeedPageAsync(
            scope,
            sourceFeedType,
            current.Cursor,
            hackerNewsReferences,
            cancellationToken).ConfigureAwait(true);
        if (!IsCurrentFeedRequest(requestId, scope, sourceFeedType) ||
            !feedPages.Complete(current, (firstPage ? page.Items : KeepDisplayedStoryPrimaries(page.Items)).Select(Localized), page.PageInfo.EndCursor, page.PageInfo.HasNextPage)) return;
        Items = feedPages.Items;
        if (feedPages.ContinueThroughEmptyPage() is not { } next) break;
        current = next;
        firstPage = false;
        NotifyPaginationChanged();
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      feedPages.Cancel(current);
      if (replace && requestId == Volatile.Read(ref loadRequestId)) Items = [];
    }
    catch (Exception ex)
    {
      if (!IsCurrentFeedRequest(requestId, scope, sourceFeedType) || !feedPages.Fail(current, ex.Message)) return;
      if (replace)
      {
        Items = [];
        ErrorMessage = ex.Message;
      }
    }
    finally
    {
      if (replace && requestId == Volatile.Read(ref loadRequestId)) IsLoading = false;
      NotifyPaginationChanged();
    }
  }

  private bool IsCurrentFeedRequest(
      int requestId,
      NewsFeedScope scope,
      NewsFeedSourceType sourceFeedType) =>
      requestId == Volatile.Read(ref loadRequestId) &&
      scope == SelectedScope &&
      sourceFeedType == SelectedSourceFeedType;

  private NewsFeedItem Localized(NewsFeedItem item) => item with { Localization = localization };

  private void NotifyPaginationChanged()
  {
    OnPropertyChanged(nameof(HasMore));
    OnPropertyChanged(nameof(IsLoadingMore));
    OnPropertyChanged(nameof(PaginationErrorMessage));
    OnPropertyChanged(nameof(HasPaginationError));
  }
}

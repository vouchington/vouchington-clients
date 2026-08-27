using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.NewsFeeds;

public sealed partial class NewsFeedsViewModel
{
  private readonly CursorPaginationState<NewsFeedItem, string> feedPages = new(item => item.Id);

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
  private async Task LoadFeedPageAsync(bool replace, CancellationToken cancellationToken)
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

    try
    {
      var page = await LoadNewsFeedPageAsync(
          scope,
          sourceFeedType,
          request.Cursor,
          cancellationToken).ConfigureAwait(true);
      if (!IsCurrentFeedRequest(requestId, scope, sourceFeedType) ||
          !feedPages.Complete(request, page.Items.Select(Localized), page.PageInfo.EndCursor, page.PageInfo.HasNextPage)) return;
      Items = feedPages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      feedPages.Cancel(request);
      if (replace && requestId == Volatile.Read(ref loadRequestId)) Items = [];
    }
    catch (Exception ex)
    {
      if (!IsCurrentFeedRequest(requestId, scope, sourceFeedType) || !feedPages.Fail(request, ex.Message)) return;
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

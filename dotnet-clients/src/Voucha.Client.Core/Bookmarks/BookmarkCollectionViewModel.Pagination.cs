using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Bookmarks;

public sealed partial class BookmarkCollectionViewModel
{
  private PageInfo? postPageInfo;
  private bool isLoadingMore;
  private string? continuationErrorMessage;
  private int continuationGuard;

  public bool IsLoadingMore
  {
    get => isLoadingMore;
    private set
    {
      if (isLoadingMore == value) return;
      isLoadingMore = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanLoadMorePosts));
      OnPropertyChanged(nameof(ShowLoadMore));
      OnPropertyChanged(nameof(CanRetryContinuation));
    }
  }

  public string? ContinuationErrorMessage
  {
    get => continuationErrorMessage;
    private set
    {
      if (continuationErrorMessage == value) return;
      continuationErrorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasContinuationError));
      OnPropertyChanged(nameof(ShowLoadMore));
      OnPropertyChanged(nameof(CanRetryContinuation));
    }
  }

  public bool HasContinuationError => !string.IsNullOrWhiteSpace(ContinuationErrorMessage);

  public bool CanLoadMorePosts =>
      Context?.Kind == BookmarkCollectionKind.Posts &&
      postPageInfo is { HasNextPage: true, EndCursor: not null } &&
      !IsLoading &&
      !IsLoadingMore;

  public bool ShowLoadMore => CanLoadMorePosts && !HasContinuationError;

  public bool CanRetryContinuation => CanLoadMorePosts && HasContinuationError;

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation failures preserve visible rows and surface retry state.")]
  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (!CanLoadMorePosts || Interlocked.CompareExchange(ref continuationGuard, 1, 0) != 0) return;
    var route = Context!;
    var generation = contextGeneration;
    var after = postPageInfo!.EndCursor!;
    var userId = sessionStore.Current.Identity?.Id;
    if (string.IsNullOrWhiteSpace(userId))
    {
      Interlocked.Exchange(ref continuationGuard, 0);
      return;
    }

    IsLoadingMore = true;
    ContinuationErrorMessage = null;
    try
    {
      var response = await client.FetchUserPostsCollectionAsync(
          userId,
          route.ListType,
          after: after,
          cancellationToken: cancellationToken).ConfigureAwait(true);
      if (!IsCurrentContext(route, generation)) return;
      AppendPostRows(response.Results, route);
      postPageInfo = response.PageInfo;
      NotifyPaginationState();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      if (IsCurrentContext(route, generation)) ContinuationErrorMessage = ex.Message;
    }
    finally
    {
      if (IsCurrentContext(route, generation))
      {
        Interlocked.Exchange(ref continuationGuard, 0);
        IsLoadingMore = false;
      }
    }
  }

  private void AppendPostRows(IReadOnlyList<Post> posts, BookmarkCollectionRouteContext route)
  {
    var existing = Rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
    var nextRank = Rows.Count == 0 ? 0 : Rows.Max(row => row.Rank) + 1;
    var additions = new List<BookmarkCollectionRow>();
    foreach (var post in posts)
    {
      if (!existing.Add(post.Id)) continue;
      additions.Add(BookmarkCollectionRowFactory.Post(post, route.InverseAction, nextRank++, localization));
    }
    if (additions.Count > 0) Rows = [.. Rows, .. additions];
  }

  private void ApplyInitialPageInfo(PageInfo? pageInfo)
  {
    postPageInfo = pageInfo;
    ContinuationErrorMessage = null;
    NotifyPaginationState();
  }

  private void ResetPagination()
  {
    postPageInfo = null;
    continuationErrorMessage = null;
    Interlocked.Exchange(ref continuationGuard, 0);
    IsLoadingMore = false;
    NotifyPaginationState();
  }

  private void NotifyPaginationState()
  {
    OnPropertyChanged(nameof(CanLoadMorePosts));
    OnPropertyChanged(nameof(ShowLoadMore));
    OnPropertyChanged(nameof(CanRetryContinuation));
    OnPropertyChanged(nameof(ContinuationErrorMessage));
    OnPropertyChanged(nameof(HasContinuationError));
  }
}

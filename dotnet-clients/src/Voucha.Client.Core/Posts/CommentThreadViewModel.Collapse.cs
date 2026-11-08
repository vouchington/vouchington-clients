using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel
{
  private readonly HashSet<string> collapseMutations = new(StringComparer.Ordinal);
  private readonly Dictionary<string, bool> collapseOverrides = new(StringComparer.Ordinal);
  private readonly Dictionary<string, CursorPaginationState<EntityReference, string>> subtreePages = new(StringComparer.Ordinal);

  public int DirectReplyCount(string postId) =>
      descendantsResponse?.PostsMetrics?.GetValueOrDefault(postId)?.Count.Children ??
      ancestorsResponse?.PostsMetrics?.GetValueOrDefault(postId)?.Count.Children ?? 0;

  public bool IsCollapseBusy(string postId) => collapseMutations.Contains(postId) ||
      subtreePages.GetValueOrDefault(postId)?.IsLoading == true;
  public bool HasMoreSubtree(string postId) => subtreePages.GetValueOrDefault(postId) is { HasMore: true };
  public bool HasSubtreeError(string postId) => subtreePages.GetValueOrDefault(postId)?.LastError is not null;

  public bool SetViewer(string? viewerId)
  {
    viewerId = string.IsNullOrWhiteSpace(viewerId) ? null : viewerId;
    if (currentUserId == viewerId) return false;
    currentUserId = viewerId;
    ++loadGeneration;
    collapsedCommentIds.Clear();
    ResetForLoad(FocusedCommentId);
    OnPropertyChanged(nameof(CurrentUserId));
    return true;
  }

  public void RestoreAnonymousCollapse(IEnumerable<string> postIds)
  {
    if (currentUserId is not null) return;
    collapsedCommentIds.Clear();
    collapsedCommentIds.UnionWith(postIds);
    OnPropertyChanged(nameof(CollapsedCommentIds));
  }

  public async Task ToggleCollapseAsync(string postId, CancellationToken cancellationToken = default)
  {
    if (IsCollapseBusy(postId) || descendantsResponse?.Posts.ContainsKey(postId) != true) return;
    if (currentUserId is null)
    {
      ToggleCollapse(postId);
      return;
    }
    var generation = loadGeneration;
    var collapse = !collapsedCommentIds.Contains(postId);
    collapseMutations.Add(postId);
    ErrorMessage = null;
    try
    {
      if (collapse)
        await postsService.BookmarkPostAsync(postId, "hide", cancellationToken).ConfigureAwait(true);
      else
        await postsService.UnbookmarkPostAsync(postId, "hide", cancellationToken).ConfigureAwait(true);
      if (generation != loadGeneration) return;
      collapseOverrides[postId] = collapse;
      SetCollapsed(postId, collapse);
      if (!collapse && !subtreePages.ContainsKey(postId))
        await LoadMoreSubtreeAsync(postId, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (generation == loadGeneration) ErrorMessage = ex.Message;
    }
    finally
    {
      if (generation == loadGeneration) collapseMutations.Remove(postId);
    }
  }

  public async Task LoadMoreSubtreeAsync(string postId, CancellationToken cancellationToken = default)
  {
    if (currentUserId is null || collapsedCommentIds.Contains(postId) || descendantsResponse?.Posts.ContainsKey(postId) != true) return;
    if (!subtreePages.TryGetValue(postId, out var pages))
    {
      pages = new(reference => EntityId(reference) ?? throw new InvalidOperationException("Comment reference requires an id."));
      subtreePages[postId] = pages;
    }
    var request = pages.BeginNextPage();
    if (request is null) return;
    var generation = loadGeneration;
    try
    {
      var response = await postsService.FetchPostDescendantsPageAsync(postId, request.Cursor, 100, cancellationToken).ConfigureAwait(true);
      if (generation != loadGeneration || !pages.Complete(request, response.Results, response.PageInfo.EndCursor, response.PageInfo.HasMorePages)) return;
      var previous = descendantsResponse!;
      descendantsResponse = MergeThreadPage(previous, response, UniqueReferences(previous.Results, response.Results)) with { PageInfo = previous.PageInfo };
      ApplyServerCollapse(response.Bookmarks);
      RebuildComments();
    }
    catch (OperationCanceledException) { pages.Cancel(request); }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (generation == loadGeneration) pages.Fail(request, ex.Message);
    }
  }

  private static EntityReference[] UniqueReferences(IReadOnlyList<EntityReference>? previous, IReadOnlyList<EntityReference> next) =>
      (previous ?? []).Concat(next).DistinctBy(EntityId).ToArray();

  private void SetCollapsed(string postId, bool collapse)
  {
    if (collapse) collapsedCommentIds.Add(postId);
    else collapsedCommentIds.Remove(postId);
    OnPropertyChanged(nameof(CollapsedCommentIds));
  }

  private void ApplyServerCollapse(IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>? bookmarks)
  {
    if (currentUserId is null || bookmarks is null) return;
    foreach (var (postId, predicates) in bookmarks)
      SetCollapsed(postId, collapseOverrides.TryGetValue(postId, out var overridden) ? overridden : predicates.GetValueOrDefault("hide"));
  }

  private void ResetCollapseRequests()
  {
    collapseMutations.Clear();
    collapseOverrides.Clear();
    subtreePages.Clear();
    if (currentUserId is not null) collapsedCommentIds.Clear();
  }
}

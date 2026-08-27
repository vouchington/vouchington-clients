using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostsListViewModel
{
  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native relation mutations surface API failures in view state before MAUI async handlers observe them.")]
  public Task ToggleSaveAsync(PostRow item, CancellationToken cancellationToken = default) =>
      ToggleBookmarkAsync(item, "save", row => row.IsSaved, (row, enabled) => row with { IsSaved = enabled }, cancellationToken);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native relation mutations surface API failures in view state before MAUI async handlers observe them.")]
  public Task ToggleHideAsync(PostRow item, CancellationToken cancellationToken = default) =>
      ToggleBookmarkAsync(item, "hide", row => row.IsHidden, (row, enabled) => row with { IsHidden = enabled }, cancellationToken);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native relation mutations surface API failures in view state before MAUI async handlers observe them.")]
  private async Task ToggleBookmarkAsync(
      PostRow item,
      string predicate,
      Func<PostRow, bool> currentState,
      Func<PostRow, bool, PostRow> applyState,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(item);
    var key = $"{item.Id}:{predicate}";
    if (!bookmarkingKeys.Add(key)) return;

    var previousItems = Items;
    var mutationLoadRequestId = requestId;
    var enabled = !currentState(item);
    Items = previousItems.Select(row => row.Id == item.Id ? applyState(row, enabled) : row).ToArray();
    ErrorMessage = null;

    try
    {
      await postsService.SetPostBookmarkAsync(item.Id, predicate, enabled, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
      RollbackBookmark(mutationLoadRequestId, previousItems, null);
    }
    catch (Exception ex)
    {
      RollbackBookmark(mutationLoadRequestId, previousItems, ex.Message);
    }
    finally
    {
      bookmarkingKeys.Remove(key);
    }
  }

  private void RollbackBookmark(
      int mutationLoadRequestId,
      IReadOnlyList<PostRow> previousItems,
      string? rollbackErrorMessage)
  {
    if (requestId != mutationLoadRequestId) return;
    Items = previousItems;
    ErrorMessage = rollbackErrorMessage;
  }
}

namespace Voucha.Client.App.Pages;

using Voucha.Client.Core.Localization;

public sealed partial class PostDetailPageBinding
{
  public bool HasMoreDescendants => viewModel.HasMoreDescendants;

  public bool IsLoadingMoreDescendants => viewModel.IsLoadingMoreDescendants;

  public bool HasDescendantPaginationError => viewModel.HasDescendantPaginationError;

  public bool HasMoreAncestors => viewModel.HasMoreAncestors;

  public bool IsLoadingMoreAncestors => viewModel.IsLoadingMoreAncestors;

  public bool HasAncestorPaginationError => viewModel.HasAncestorPaginationError;

  public bool CanLoadMoreAncestors => !viewModel.IsLoadingMoreAncestors;

  public string AncestorPaginationLabel => localization.Localize(
      viewModel.HasAncestorPaginationError
          ? UiMessageKey.NativeCommonRetry
          : viewModel.IsLoadingMoreAncestors
              ? UiMessageKey.NativeSwiftCommonLoadingMore
              : UiMessageKey.ExtractedCommentsCommentAncestorTrailShowEarlierReplies56b87971);

  public async Task LoadMoreDescendantsAsync(CancellationToken cancellationToken = default)
  {
    await viewModel.LoadMoreDescendantsAsync(cancellationToken).ConfigureAwait(true);
    RefreshRows();
  }

  public async Task LoadMoreAncestorsAsync(CancellationToken cancellationToken = default)
  {
    var loading = viewModel.LoadMoreAncestorsAsync(cancellationToken);
    OnPropertyChanged(nameof(CanLoadMoreAncestors));
    OnPropertyChanged(nameof(AncestorPaginationLabel));
    await loading.ConfigureAwait(true);
    RefreshRows();
  }

  public async Task ToggleCollapseAsync(string postId, CancellationToken cancellationToken = default)
  {
    var viewer = CurrentUserId;
    if (viewModel.SetViewer(viewer))
    {
      await LoadAsync(viewModel.FocusedCommentId, cancellationToken).ConfigureAwait(true);
      return;
    }
    var mutation = viewModel.ToggleCollapseAsync(postId, cancellationToken);
    RefreshRows();
    await mutation.ConfigureAwait(true);
    if (viewer != CurrentUserId) return;
    if (viewModel.CollapseStateKey is { } key) collapseStore?.Save(key, viewModel.CollapsedCommentIds);
    RefreshRows();
  }

  public async Task LoadMoreSubtreeAsync(string postId, CancellationToken cancellationToken = default)
  {
    if (viewModel.SetViewer(CurrentUserId))
    {
      await LoadAsync(viewModel.FocusedCommentId, cancellationToken).ConfigureAwait(true);
      return;
    }
    var request = viewModel.LoadMoreSubtreeAsync(postId, cancellationToken);
    RefreshRows();
    await request.ConfigureAwait(true);
    RefreshRows();
  }

  private void RestoreAnonymousCollapse()
  {
    if (collapseStore is not null && viewModel.CollapseStateKey is { } key)
      viewModel.RestoreAnonymousCollapse(collapseStore.Load(key));
  }
}

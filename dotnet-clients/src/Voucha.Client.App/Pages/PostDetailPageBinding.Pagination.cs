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
}

namespace Voucha.Client.App.Pages;

public sealed partial class PostDetailPageBinding
{
  public bool HasMoreDescendants => viewModel.HasMoreDescendants;

  public bool IsLoadingMoreDescendants => viewModel.IsLoadingMoreDescendants;

  public bool HasDescendantPaginationError => viewModel.HasDescendantPaginationError;

  public async Task LoadMoreDescendantsAsync(CancellationToken cancellationToken = default)
  {
    await viewModel.LoadMoreDescendantsAsync(cancellationToken).ConfigureAwait(true);
    RefreshRows();
  }
}

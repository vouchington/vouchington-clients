namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private void OnRemainingItemsThresholdReached(object? sender, EventArgs args) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs args) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);
}

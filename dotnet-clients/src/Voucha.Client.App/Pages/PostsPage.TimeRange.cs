namespace Voucha.Client.App.Pages;

public partial class PostsPage
{
  private async void OnTimeRangeSelected(object? sender, string value) =>
      await viewModel.SelectBrowseTimeRangeAsync(value).ConfigureAwait(true);
}

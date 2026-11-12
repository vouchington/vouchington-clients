namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private async void OnHackerNewsFilterClicked(object? sender, EventArgs e)
  {
    await viewModel.ToggleHackerNewsFilterAsync();
  }

}

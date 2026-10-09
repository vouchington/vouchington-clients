namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private async void OnLoadMoreOAuthGrantsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreOAuthGrantsAsync().ConfigureAwait(true);
    RearmSettingsPagination(sender);
  }
}

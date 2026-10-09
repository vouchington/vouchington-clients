using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private async void OnRevokeOAuthGrantClicked(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: SettingsOAuthGrantRow row })
      await viewModel.RevokeOAuthGrantAsync(row.ProtocolValue).ConfigureAwait(true);
  }

  private async void OnLoadMoreOAuthGrantsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreOAuthGrantsAsync().ConfigureAwait(true);
    RearmSettingsPagination(sender);
  }
}

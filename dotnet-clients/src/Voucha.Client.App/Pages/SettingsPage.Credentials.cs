using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private void OnApiKeyScopeChanged(object? sender, CheckedChangedEventArgs args)
  {
    if (sender is CheckBox { BindingContext: SettingsScopeRow row } && args.Value != row.IsSelected)
      viewModel.SetApiKeyScopeSelected(row.ProtocolValue.Scope, args.Value);
  }

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

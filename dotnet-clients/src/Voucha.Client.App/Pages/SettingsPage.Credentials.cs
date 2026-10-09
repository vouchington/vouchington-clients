using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private async void OnRevokeOAuthGrantClicked(object? sender, EventArgs args)
  {
    if (sender is not Button { CommandParameter: SettingsOAuthGrantRow row })
      return;

    await OAuthGrantRevocationConfirmation.RunAsync(
        row.ProtocolValue.Id,
        row.ClientName,
        UiCopy.CurrentLocalization,
        (title, message, accept, cancel) => DisplayAlertAsync(title, message, accept, cancel),
        async _ => await viewModel.RevokeOAuthGrantAsync(row.ProtocolValue).ConfigureAwait(true));
  }

  private async void OnLoadMoreOAuthGrantsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreOAuthGrantsAsync().ConfigureAwait(true);
    RearmSettingsPagination(sender);
  }
}

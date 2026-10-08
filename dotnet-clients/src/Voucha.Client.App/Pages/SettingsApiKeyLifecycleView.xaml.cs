using Voucha.Client.Core.Settings;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Voucha.Client.App.Pages;

public partial class SettingsApiKeyLifecycleView : ContentView
{
  public SettingsApiKeyLifecycleView() => InitializeComponent();

  private void OnApiKeyScopeChanged(object? sender, CheckedChangedEventArgs args)
  {
    if (BindingContext is SettingsViewModel viewModel &&
        sender is CheckBox { BindingContext: SettingsScopeRow row } && args.Value != row.IsSelected)
      viewModel.SetApiKeyScopeSelected(row.ProtocolValue.Scope, args.Value);
  }

  private async void OnCreateApiKeyClicked(object? sender, EventArgs e)
  {
    if (BindingContext is SettingsViewModel viewModel) await viewModel.CreateApiKeyAsync();
  }

  private async void OnCopyApiKeyClicked(object? sender, EventArgs e)
  {
    if (BindingContext is SettingsViewModel { ApiKeySecret: { Length: > 0 } secret })
      await Clipboard.SetTextAsync(secret);
  }

  private void OnDismissApiKeySecretClicked(object? sender, EventArgs e)
  {
    if (BindingContext is SettingsViewModel viewModel) viewModel.DismissApiKeySecret();
  }

  private async void OnRotateApiKeyClicked(object? sender, EventArgs e)
  {
    if (BindingContext is SettingsViewModel viewModel &&
        sender is Button { CommandParameter: SettingsApiKeyRow row })
      await viewModel.RotateApiKeyAsync(row.ProtocolValue);
  }

  private async void OnRevokeApiKeyClicked(object? sender, EventArgs e)
  {
    if (BindingContext is SettingsViewModel viewModel &&
        sender is Button { CommandParameter: SettingsApiKeyRow row })
      await viewModel.RevokeApiKeyAsync(row.ProtocolValue);
  }
}

using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public partial class SettingsOAuthGrantsView : ContentView
{
  public SettingsOAuthGrantsView() => InitializeComponent();

  internal Func<string, string, string, string, Task<bool>>? ConfirmRevocationAsync { get; set; }

  public event EventHandler? LoadNextPageRequested;

  private async void OnRevokeOAuthGrantClicked(object? sender, EventArgs args)
  {
    if (BindingContext is not SettingsViewModel viewModel ||
        sender is not Button { CommandParameter: SettingsOAuthGrantRow row }) return;

    await OAuthGrantRevocationConfirmation.RunAsync(
        row.ProtocolValue.Id,
        row.ClientName,
        UiCopy.CurrentLocalization,
        ConfirmRevocationAsync ?? ConfirmOnParentPageAsync,
        async _ => await viewModel.RevokeOAuthGrantAsync(row.ProtocolValue).ConfigureAwait(true));
  }

  private Task<bool> ConfirmOnParentPageAsync(string title, string message, string accept, string cancel)
  {
    Element? parent = Parent;
    while (parent is not null && parent is not Page) parent = parent.Parent;
    return parent is Page page
        ? page.DisplayAlertAsync(title, message, accept, cancel)
        : Task.FromResult(false);
  }

  private void OnLoadNextPageRequested(object? sender, EventArgs args) =>
      LoadNextPageRequested?.Invoke(sender, args);
}

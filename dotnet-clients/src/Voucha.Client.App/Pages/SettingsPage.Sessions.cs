using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private async void OnRevokeSessionClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: AuthSession session })
    {
      return;
    }

    var confirmed = await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetSettingsSignOut),
        UiCopy.Localize(session.IsCurrent
            ? UiMessageKey.NativeDotnetDynamicSignOutDeviceQuestion
            : UiMessageKey.NativeDotnetDynamicSignOutSessionQuestion),
        UiCopy.Localize(UiMessageKey.NativeDotnetSettingsSignOut),
        UiCopy.Localize(UiMessageKey.CommonCancel));
    if (!confirmed)
    {
      return;
    }

    if (await viewModel.RevokeSessionAsync(session))
    {
      await sessionStore.SignOutAsync();
      await Shell.Current.GoToAsync("//session");
    }
  }

  private async void OnRevokeAllSessionsClicked(object? sender, EventArgs e)
  {
    var confirmed = await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetDynamicSignOutAll),
        UiCopy.Localize(UiMessageKey.NativeDotnetDynamicSignOutAllQuestion),
        UiCopy.Localize(UiMessageKey.NativeDotnetDynamicSignOutAll),
        UiCopy.Localize(UiMessageKey.CommonCancel));
    if (!confirmed)
    {
      return;
    }

    if (await viewModel.RevokeAllSessionsAsync())
    {
      await sessionStore.SignOutAsync();
      await Shell.Current.GoToAsync("//session");
    }
  }

  private async Task SignOutAndNavigateAsync()
  {
    await sessionStore.SignOutAsync();
    await Shell.Current.GoToAsync("//session");
  }
}

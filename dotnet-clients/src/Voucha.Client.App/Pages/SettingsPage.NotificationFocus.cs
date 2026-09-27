using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private bool notificationSettingsFocused;
  private Task<bool>? notificationPreferencesLoadTask;
  private readonly NotificationSettingsRouteEpoch notificationRouteEpoch = new();

  public void ApplyInitialRouteMatch(NativeRouteMatch? match)
  {
    var notificationRouteApplied = string.Equals(
        match?.Path,
        "/my/notification-settings",
        StringComparison.Ordinal);
    notificationRouteEpoch.Apply(notificationRouteApplied);
    if (notificationRouteApplied) notificationAccessibility.Deactivate();
    notificationSettingsFocused = notificationRouteApplied;
    ApplyNotificationSettingsFocus();
  }

  public Task ApplyRouteMatchAsync(NativeRouteMatch? match)
  {
    ApplyInitialRouteMatch(match);
    return isPageVisible
        ? LoadNotificationPreferencesForFocusedRouteAsync()
        : Task.CompletedTask;
  }

  private void ApplyNotificationSettingsFocus()
  {
    UpdateBlueskySectionVisibility();
    OAuthAccountsSection.IsVisible = !notificationSettingsFocused;
    NotificationPreferencesSection.IsVisible = notificationSettingsFocused;
    IdentitySection.IsVisible = !notificationSettingsFocused;
    EmailAddressManager.IsVisible = !notificationSettingsFocused;
    ProfileSection.IsVisible = !notificationSettingsFocused;
    ProfileLinksSection.IsVisible = !notificationSettingsFocused;
    PrivacySection.IsVisible = !notificationSettingsFocused;
    ApiKeysSection.IsVisible = !notificationSettingsFocused;
    ConnectedAppsSection.IsVisible = !notificationSettingsFocused;
    LegalSupportSection.IsVisible = !notificationSettingsFocused;
    MembershipSection.IsVisible = !notificationSettingsFocused;
    SessionsSection.IsVisible = !notificationSettingsFocused;
    DataSection.IsVisible = !notificationSettingsFocused;
    PushSubscriptionsSection.IsVisible = !notificationSettingsFocused;
    if (notificationSettingsFocused)
    {
      LocalLLMSection.RemoveBinding(IsVisibleProperty);
      LocalLLMSection.IsVisible = false;
    }
    else
    {
      LocalLLMSection.SetBinding(IsVisibleProperty, nameof(SettingsViewModel.LocalLLMSettingsAvailable));
    }

    if (!notificationSettingsFocused)
    {
      notificationAccessibility.Deactivate();
      return;
    }

  }

  private async Task LoadNotificationPreferencesForFocusedRouteAsync()
  {
    if (!notificationSettingsFocused || !isPageVisible) return;
    var routeEpoch = notificationRouteEpoch.Value;
    var loadTask = notificationPreferencesLoadTask;
    if (loadTask is null || loadTask.IsCompleted)
    {
      loadTask = RunNotificationPreferencesLoadAsync();
      notificationPreferencesLoadTask = loadTask;
    }

    var loaded = await loadTask.ConfigureAwait(true);
    if (ReferenceEquals(notificationPreferencesLoadTask, loadTask)) notificationPreferencesLoadTask = null;
    if (!loaded || routeEpoch != notificationRouteEpoch.Value || !notificationSettingsFocused || !isPageVisible) return;
    Dispatcher.Dispatch(() =>
    {
      if (routeEpoch != notificationRouteEpoch.Value || !notificationSettingsFocused || !isPageVisible) return;
      notificationAccessibility.Deactivate();
      notificationAccessibility.Activate(NotificationPreferencesSection.Heading);
    });
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "The route lifecycle must observe and recover from every preference-load failure.")]
  private async Task<bool> RunNotificationPreferencesLoadAsync()
  {
    await SettingsScroll.ScrollToAsync(NotificationPreferencesSection, ScrollToPosition.Start, false)
        .ConfigureAwait(true);

    while (notificationSettingsFocused && isPageVisible)
    {
      try
      {
        await notificationPreferences.LoadAsync().ConfigureAwait(true);
        if (!notificationSettingsFocused || !isPageVisible) return false;
        NotificationPreferencesSection.SynchronizeControls();
        return true;
      }
      catch (Exception ex)
      {
        if (!notificationSettingsFocused || !isPageVisible) return false;
        System.Diagnostics.Debug.WriteLine(ex);
        var retry = await DisplayAlertAsync(
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpActionFailed),
            UiCopy.Resolve(UiText.ExternalContent(ex.Message)),
            UiCopy.Localize(UiMessageKey.NativeCommonRetry),
            UiCopy.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true);
        if (!notificationSettingsFocused || !isPageVisible || !retry) return false;
      }
    }
    return false;
  }
}

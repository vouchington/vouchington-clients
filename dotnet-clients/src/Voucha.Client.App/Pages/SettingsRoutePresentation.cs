namespace Voucha.Client.App.Pages;

internal static class SettingsRoutePresentation
{
  internal static bool ShowsBlueskySection(bool fediverseEnabled, bool notificationSettingsFocused) =>
      fediverseEnabled && !notificationSettingsFocused;
}

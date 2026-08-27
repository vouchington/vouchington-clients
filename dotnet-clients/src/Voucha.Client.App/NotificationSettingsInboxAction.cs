namespace Voucha.Client.App;

internal static class NotificationSettingsInboxAction
{
  private const string SettingsRoute = "/my/notification-settings";

  public static Task OpenAsync(AppShell appShell)
  {
    ArgumentNullException.ThrowIfNull(appShell);
    return appShell.OpenNativePathAsync(SettingsRoute);
  }
}

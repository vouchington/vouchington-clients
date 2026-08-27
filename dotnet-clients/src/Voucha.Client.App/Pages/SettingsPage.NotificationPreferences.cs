namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private async void OnNotificationPreferenceChanged(
      object? sender,
      NotificationPreferenceChangedEventArgs e)
  {
    try
    {
      switch (e.Field)
      {
        case "engagement": await notificationPreferences.SetEngagementEmailsAsync((bool)e.Value!); break;
        case "news": await notificationPreferences.SetNewsDigestAsync((string)e.Value!); break;
        case "moderation": await notificationPreferences.SetModerationEmailsAsync((bool)e.Value!); break;
        case "community": await notificationPreferences.SetCommunityDigestAsync((string)e.Value!); break;
        case "cadence": await notificationPreferences.SetCadenceAsync((string)e.Value!); break;
        case "time": await notificationPreferences.SetTimeAsync((TimeSpan)e.Value!); break;
        case "timezone": await notificationPreferences.SetTimezoneAsync((string)e.Value!); break;
        case "day":
          var days = notificationPreferences.ModerationEmailDaysOfWeek.ToHashSet();
          if (!days.Remove((int)e.Value!)) days.Add((int)e.Value!);
          await notificationPreferences.SetDaysAsync(days.Order().ToArray());
          break;
      }
      NotificationPreferencesSection.SynchronizeControls();
    }
    catch (Exception)
    {
      NotificationPreferencesSection.SynchronizeControls();
    }
  }
}

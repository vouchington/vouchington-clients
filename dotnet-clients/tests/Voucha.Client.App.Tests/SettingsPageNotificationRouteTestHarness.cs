using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public sealed partial class SettingsPage
{
  // Route-only tests exercise the production focus lifecycle before page appearance or loading.
  private readonly NotificationPreferencesViewModel notificationPreferences = null!;
  private readonly INotificationSettingsAccessibilityCoordinator notificationAccessibility = new RouteAccessibility();
  private readonly bool isPageVisible = false;

  public bool NotificationSectionVisibleForTest => NotificationPreferencesSection.IsVisible;

  private void UpdateBlueskySectionVisibility() { }

  private sealed class RouteAccessibility : INotificationSettingsAccessibilityCoordinator
  {
    public void Activate(VisualElement heading) { }
    public void Deactivate() { }
  }
}

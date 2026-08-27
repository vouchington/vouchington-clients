using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Navigation;

public static partial class NavigationCatalog
{
  private static NavIntent SettingsIntent { get; } = new(
      SettingsIntentId,
      UiMessageKey.ExtractedIntentsProductSettingsSettings74a883a0,
      "settings-2",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSettingsAccount7e1b0d56,
            "sidebar-group-account",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSettingsIdentity999f23fc, "/my/identity", "sidebar-nav-my-identity", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSettingsProfileD696a35b, "/my/profile", "sidebar-nav-my-profile", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSettingsPrivacy54a57c31, "/my/privacy", "sidebar-nav-my-privacy", RequiresAuth: true),
              new NavItem(UiMessageKey.NativeDotnetSettingsCommunityMemberships, "/my/household", "sidebar-nav-my-household", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.NativeDotnetSettingsFinancial,
            "sidebar-group-financial",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSettingsMembership9feceb93, "/my/membership", "sidebar-nav-my-membership", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductCommunicationNotifications78801183,
            "sidebar-group-notifications",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductCommunicationNotifications78801183, "/my/notification-settings", "sidebar-nav-my-notification-settings", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSettingsAdvanced9f088dbe,
            "sidebar-group-advanced",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSettingsApiKeysC08f17eb, "/my/api-keys", "sidebar-nav-my-api-keys", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductCommunicationNotifications78801183, "/my/notifications/push-subscriptions", "sidebar-nav-my-push-subscriptions", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSettingsYourData0fdcada4, "/my/data", "sidebar-nav-my-data", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ],
      RequiresAuth: true);
}

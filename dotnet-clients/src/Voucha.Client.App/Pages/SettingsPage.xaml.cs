using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage : ContentPage
{
  private readonly SettingsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly IServiceProvider serviceProvider;
  private readonly EmailAddressManagerViewModel emailAddressViewModel;
  private readonly NativeBlueskyLinkCoordinator blueskyCoordinator;
  private readonly INavigationViewerProvider navigationViewerProvider;
  private readonly NotificationPreferencesViewModel notificationPreferences;
  private readonly INotificationSettingsAccessibilityCoordinator notificationAccessibility;

  public SettingsPage(
      SettingsViewModel viewModel,
      ISessionStore sessionStore,
      IServiceProvider serviceProvider,
      VouchaApiClient apiClient,
      IEmailAddressService emailAddressService,
      IUiLocalization localization,
      IUiLocaleController localeController,
      NotificationPreferencesViewModel notificationPreferences,
      INotificationSettingsAccessibilityCoordinator notificationAccessibility,
      NativeBlueskyLinkCoordinator blueskyCoordinator,
      NativeOAuthAuthorizationCoordinator oauthCoordinator,
      INavigationViewerProvider navigationViewerProvider)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.serviceProvider = serviceProvider;
    this.blueskyCoordinator = blueskyCoordinator;
    this.oauthCoordinator = oauthCoordinator;
    this.navigationViewerProvider = navigationViewerProvider;
    this.notificationPreferences = notificationPreferences;
    this.notificationAccessibility = notificationAccessibility;
    emailAddressViewModel = new EmailAddressManagerViewModel(
        emailAddressService,
        localization,
        localeController);
    EmailAddressManager.BindingContext = emailAddressViewModel;
    NotificationPreferencesSection.BindingContext = notificationPreferences;
    NotificationPreferencesSection.ConfigureLocalization(localization, localeController);
    NotificationPreferencesSection.PreferenceChanged += OnNotificationPreferenceChanged;
    ProfileMarkdownEditor.ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    BindingContext = viewModel;
  }

  private async void OnRefreshClicked(object? sender, EventArgs e)
  {
    await viewModel.LoadAsync();
    ApplyNotificationSettingsFocus();
    await LoadNotificationPreferencesForFocusedRouteAsync().ConfigureAwait(true);
    EnsureDataRequestPolling();
  }

  private async void OnSaveIdentityClicked(object? sender, EventArgs e)
  {
    await viewModel.SaveIdentityAsync();
  }

  private async void OnClearAvatarClicked(object? sender, EventArgs e)
  {
    await viewModel.ClearAvatarAsync();
  }

  private async void OnSaveProfileClicked(object? sender, EventArgs e)
  {
    await viewModel.SaveProfileAsync();
  }

  private async void OnOpenProfileClicked(object? sender, EventArgs e)
  {
    await Navigation.PushAsync(serviceProvider.GetRequiredService<ProfilePage>());
  }

  private async void OnSaveProfileLinkClicked(object? sender, EventArgs e)
  {
    await viewModel.SaveProfileLinkAsync();
  }

  private void OnClearProfileLinkDraftClicked(object? sender, EventArgs e)
  {
    viewModel.ResetProfileLinkDraft();
  }

  private void OnEditProfileLinkClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: SettingsProfileLinkRow row })
    {
      viewModel.EditProfileLink(row.ProtocolValue);
    }
  }

  private async void OnMoveProfileLinkUpClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: SettingsProfileLinkRow row })
    {
      await viewModel.MoveProfileLinkAsync(row.ProtocolValue, -1);
    }
  }

  private async void OnMoveProfileLinkDownClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: SettingsProfileLinkRow row })
    {
      await viewModel.MoveProfileLinkAsync(row.ProtocolValue, 1);
    }
  }

  private async void OnDeleteProfileLinkClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: SettingsProfileLinkRow row })
    {
      await viewModel.DeleteProfileLinkAsync(row.ProtocolValue);
    }
  }

  private async void OnPrivacySelectionChanged(object? sender, EventArgs e)
  {
    if (viewModel.IsLoading)
    {
      return;
    }

    if (sender is Picker { IsFocused: true, BindingContext: SettingsSelectionRowViewModel row })
    {
      await viewModel.UpdatePrivacySelectionAsync(row);
    }
  }

  private async void OnPrivacyToggleChanged(object? sender, ToggledEventArgs e)
  {
    if (viewModel.IsLoading)
    {
      return;
    }

    if (sender is Switch { IsFocused: true, BindingContext: SettingsToggleRowViewModel row })
    {
      await viewModel.UpdatePrivacyToggleAsync(row);
      if (sessionStore is IForcedSessionRefreshStore forced)
      {
        await forced.RefreshAsync(force: true);
      }
    }
  }

  private async void OnRevokePushSubscriptionClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: WebPushSubscription subscription })
    {
      await viewModel.DeletePushSubscriptionAsync(subscription);
    }
  }

}

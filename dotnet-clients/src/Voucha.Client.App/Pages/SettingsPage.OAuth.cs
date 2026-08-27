using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private readonly NativeOAuthAuthorizationCoordinator oauthCoordinator;
  private bool oauthObserversAttached;
  private bool consumingOAuthResult;

  private void AttachOAuthObservers()
  {
    if (oauthObserversAttached) return;
    oauthCoordinator.PropertyChanged += OnOAuthStateChanged;
    sessionStore.SessionChanged += OnOAuthSessionChanged;
    oauthObserversAttached = true;
  }

  private void DetachOAuthObservers()
  {
    if (!oauthObserversAttached) return;
    oauthCoordinator.PropertyChanged -= OnOAuthStateChanged;
    sessionStore.SessionChanged -= OnOAuthSessionChanged;
    oauthObserversAttached = false;
  }

  private void OnOAuthStateChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
      MainThread.BeginInvokeOnMainThread(() => _ = HandleOAuthStateChangedAsync());

  private async Task HandleOAuthStateChangedAsync()
  {
    RefreshOAuthAccountsPresentation();
    await ConsumeOAuthResultAsync().ConfigureAwait(true);
  }

  private void OnOAuthSessionChanged(object? sender, SessionChangedEventArgs eventArgs) =>
      MainThread.BeginInvokeOnMainThread(RefreshOAuthAccountsPresentation);

  private async Task RefreshOAuthAccountsAsync()
  {
    if (!OAuthAccountsSection.IsVisible) return;
    await oauthCoordinator.LoadCapabilitiesAsync().ConfigureAwait(true);
    await oauthCoordinator.ResumeAsync().ConfigureAwait(true);
    RefreshOAuthAccountsPresentation();
    await ConsumeOAuthResultAsync().ConfigureAwait(true);
  }

  private void RefreshOAuthAccountsPresentation()
  {
    if (!OAuthAccountsSection.IsVisible) return;
    var identity = sessionStore.Current.Identity;
    ConfigureOAuthButton(
        FacebookOAuthAccountButton,
        OAuthBrokerProvider.Facebook,
        oauthCoordinator.IsProviderConnected(OAuthBrokerProvider.Facebook));
    ConfigureOAuthButton(
        XOAuthAccountButton,
        OAuthBrokerProvider.X,
        oauthCoordinator.IsProviderConnected(OAuthBrokerProvider.X));
    ConfigureOAuthButton(
        GithubOAuthAccountButton,
        OAuthBrokerProvider.Github,
        oauthCoordinator.IsProviderConnected(OAuthBrokerProvider.Github));
    FacebookOAuthIdentity.Text = identity?.FacebookAccount?.Name ?? string.Empty;
    XOAuthIdentity.Text = identity?.XAccount?.Name ?? string.Empty;
    GithubOAuthIdentity.Text = identity?.GithubAccount?.Name ?? string.Empty;
    RetryOAuthAccountCapabilitiesButton.IsVisible = oauthCoordinator.CanRetryCapabilityLoading;
    CancelOAuthAccountButton.IsVisible = oauthCoordinator.CanCancelPendingAuthorization;
    OAuthAccountsStatus.Text = oauthCoordinator.State switch
    {
      NativeOAuthAuthorizationState.Finalizing =>
          UiCopy.Localize(UiMessageKey.NativeSwiftSettingsOauthFinalizing),
      NativeOAuthAuthorizationState.Expired =>
          UiCopy.Localize(UiMessageKey.NativeSwiftSettingsOauthExpired),
      NativeOAuthAuthorizationState.Failed =>
          UiCopy.Localize(UiMessageKey.NativeSwiftSettingsOauthFailed),
      _ => string.Empty,
    };
  }

  private void ConfigureOAuthButton(Button button, OAuthBrokerProvider provider, bool connected)
  {
    button.Text = UiCopy.Localize(
        connected
            ? UiMessageKey.NativeSwiftSettingsBlueskyDisconnect
            : UiMessageKey.NativeSwiftSettingsBlueskyConnect);
    button.IsVisible = connected ||
        oauthCoordinator.Supports(provider, OAuthAuthorizationPurpose.Connect);
    button.IsEnabled =
        oauthCoordinator.Pending is null &&
        oauthCoordinator.Result is null &&
        oauthCoordinator.State is not
            NativeOAuthAuthorizationState.Connecting and not
            NativeOAuthAuthorizationState.Finalizing and not
            NativeOAuthAuthorizationState.Disconnecting;
  }

  private async Task ConsumeOAuthResultAsync()
  {
    if (consumingOAuthResult ||
        oauthCoordinator.Result is not { Purpose: OAuthAuthorizationPurpose.Connect } result)
    {
      return;
    }
    if (result.Kind == NativeOAuthAuthorizationResultKind.Expired) return;
    consumingOAuthResult = true;
    try
    {
      RefreshOAuthAccountsPresentation();
      await oauthCoordinator.AcknowledgeResultAsync().ConfigureAwait(true);
    }
    finally
    {
      consumingOAuthResult = false;
    }
  }

  private async void OnOAuthAccountClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: string providerValue }) return;
    var provider = ParseProvider(providerValue);
    if (IsConnected(provider))
    {
      await oauthCoordinator.DisconnectAsync(provider).ConfigureAwait(true);
    }
    else
    {
      await oauthCoordinator
          .StartAsync(provider, OAuthAuthorizationPurpose.Connect)
          .ConfigureAwait(true);
    }
  }

  private async void OnRetryOAuthCapabilitiesClicked(object? sender, EventArgs e) =>
      await oauthCoordinator.LoadCapabilitiesAsync().ConfigureAwait(true);

  private async void OnCancelOAuthAccountClicked(object? sender, EventArgs e) =>
      await oauthCoordinator.CancelAsync().ConfigureAwait(true);

  private bool IsConnected(OAuthBrokerProvider provider) =>
      oauthCoordinator.IsProviderConnected(provider);

  private static OAuthBrokerProvider ParseProvider(string provider) => provider switch
  {
    "facebook" => OAuthBrokerProvider.Facebook,
    "x" => OAuthBrokerProvider.X,
    "github" => OAuthBrokerProvider.Github,
    _ => throw new ArgumentOutOfRangeException(nameof(provider)),
  };
}

using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class AuthPage
{
  private bool oauthObserversAttached;
  private bool consumingOAuthResult;

  private void AttachOAuthObservers()
  {
    if (oauthObserversAttached) return;
    oauthCoordinator.PropertyChanged += OnOAuthStateChanged;
    oauthObserversAttached = true;
  }

  private void DetachOAuthObservers()
  {
    if (!oauthObserversAttached) return;
    oauthCoordinator.PropertyChanged -= OnOAuthStateChanged;
    oauthObserversAttached = false;
  }

  private void OnOAuthStateChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
      MainThread.BeginInvokeOnMainThread(() => _ = HandleOAuthStateChangedAsync());

  private async Task HandleOAuthStateChangedAsync()
  {
    RefreshOAuthPresentation();
    await ConsumeOAuthResultAsync().ConfigureAwait(true);
  }

  private async Task RefreshOAuthAsync()
  {
    await oauthCoordinator.LoadCapabilitiesAsync().ConfigureAwait(true);
    await oauthCoordinator.ResumeAsync().ConfigureAwait(true);
    RefreshOAuthPresentation();
    await ConsumeOAuthResultAsync().ConfigureAwait(true);
  }

  private void RefreshOAuthPresentation()
  {
    FacebookOAuthButton.IsVisible = oauthCoordinator.Supports(
        OAuthBrokerProvider.Facebook,
        OAuthAuthorizationPurpose.Authenticate);
    XOAuthButton.IsVisible = oauthCoordinator.Supports(
        OAuthBrokerProvider.X,
        OAuthAuthorizationPurpose.Authenticate);
    GithubOAuthButton.IsVisible = oauthCoordinator.Supports(
        OAuthBrokerProvider.Github,
        OAuthAuthorizationPurpose.Authenticate);
    var canStart = oauthCoordinator.Pending is null &&
        oauthCoordinator.Result is null &&
        oauthCoordinator.State is not
            NativeOAuthAuthorizationState.Connecting and not
            NativeOAuthAuthorizationState.Finalizing;
    FacebookOAuthButton.IsEnabled = canStart;
    XOAuthButton.IsEnabled = canStart;
    GithubOAuthButton.IsEnabled = canStart;
    RetryOAuthCapabilitiesButton.IsVisible = oauthCoordinator.CanRetryCapabilityLoading;
    CancelOAuthButton.IsVisible = oauthCoordinator.CanCancelPendingAuthorization;
    OAuthStatus.Text = oauthCoordinator.State switch
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

  private async Task ConsumeOAuthResultAsync()
  {
    if (consumingOAuthResult || oauthCoordinator.Result is not { Purpose: OAuthAuthorizationPurpose.Authenticate } result)
    {
      return;
    }
    if (result.Kind == NativeOAuthAuthorizationResultKind.Expired) return;
    consumingOAuthResult = true;
    try
    {
      if (result.Kind == NativeOAuthAuthorizationResultKind.MfaRequired &&
          result.LoginAttemptId is { Length: > 0 } loginAttemptId)
      {
        viewModel.AcceptMfaChallenge(loginAttemptId);
      }
      await oauthCoordinator.AcknowledgeResultAsync().ConfigureAwait(true);
    }
    finally
    {
      consumingOAuthResult = false;
    }
  }

  private Task StartOAuthAsync(OAuthBrokerProvider provider) =>
      oauthCoordinator.StartAsync(provider, OAuthAuthorizationPurpose.Authenticate);

  private async void OnRetryOAuthCapabilitiesClicked(object? sender, EventArgs e) =>
      await oauthCoordinator.LoadCapabilitiesAsync().ConfigureAwait(true);

  private async void OnFacebookOAuthClicked(object? sender, EventArgs e) =>
      await StartOAuthAsync(OAuthBrokerProvider.Facebook).ConfigureAwait(true);

  private async void OnXOAuthClicked(object? sender, EventArgs e) =>
      await StartOAuthAsync(OAuthBrokerProvider.X).ConfigureAwait(true);

  private async void OnGithubOAuthClicked(object? sender, EventArgs e) =>
      await StartOAuthAsync(OAuthBrokerProvider.Github).ConfigureAwait(true);

  private async void OnCancelOAuthClicked(object? sender, EventArgs e)
  {
    await oauthCoordinator.CancelAsync().ConfigureAwait(true);
    if (oauthCoordinator.State == NativeOAuthAuthorizationState.Cancelled)
    {
      viewModel.ClearMfaChallenge();
    }
  }
}

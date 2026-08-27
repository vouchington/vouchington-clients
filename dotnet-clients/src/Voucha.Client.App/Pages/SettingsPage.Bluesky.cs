using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using System.ComponentModel;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private bool blueskyObserversAttached;

  private void AttachBlueskyObservers()
  {
    if (blueskyObserversAttached) return;
    blueskyCoordinator.PropertyChanged += OnBlueskyStateChanged;
    sessionStore.SessionChanged += OnBlueskySessionChanged;
    blueskyObserversAttached = true;
  }

  private void DetachBlueskyObservers()
  {
    if (!blueskyObserversAttached) return;
    blueskyCoordinator.PropertyChanged -= OnBlueskyStateChanged;
    sessionStore.SessionChanged -= OnBlueskySessionChanged;
    blueskyObserversAttached = false;
  }

  private void OnBlueskyStateChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
      MainThread.BeginInvokeOnMainThread(RefreshBlueskyPresentation);

  private void OnBlueskySessionChanged(object? sender, SessionChangedEventArgs eventArgs) =>
      MainThread.BeginInvokeOnMainThread(RefreshBlueskyPresentation);

  private async Task RefreshBlueskyAsync()
  {
    UpdateBlueskySectionVisibility();
    if (!BlueskySection.IsVisible) return;
    await blueskyCoordinator.ResumeAsync(DateTimeOffset.UtcNow).ConfigureAwait(true);
    RefreshBlueskyPresentation();
  }

  private void RefreshBlueskyPresentation()
  {
    UpdateBlueskySectionVisibility();
    if (!BlueskySection.IsVisible) return;
    BlueskyLinkedHandle.Text = blueskyCoordinator.LinkedHandle;
    BlueskyDisconnectButton.IsVisible = blueskyCoordinator.IsLinked;
    BlueskyConnectButton.IsVisible =
        !blueskyCoordinator.IsLinked &&
        blueskyCoordinator.State is
            NativeBlueskyLinkState.Idle or
            NativeBlueskyLinkState.Cancelled or
            NativeBlueskyLinkState.Expired or
            NativeBlueskyLinkState.Failed;
    BlueskyCancelButton.IsVisible = blueskyCoordinator.State == NativeBlueskyLinkState.WaitingForCallback;
    ApplyBlueskyStateCopy();
  }

  private void UpdateBlueskySectionVisibility()
  {
    BlueskySection.IsVisible = SettingsRoutePresentation.ShowsBlueskySection(
        navigationViewerProvider.CurrentViewer.FeatureFlags?.GetValueOrDefault("fediverse") == true,
        notificationSettingsFocused);
  }

  private async void OnConnectBlueskyClicked(object? sender, EventArgs e)
  {
    if (string.IsNullOrWhiteSpace(BlueskyHandleEntry.Text))
    {
      BlueskyStatus.Text = UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyHandleRequired);
      return;
    }
    try
    {
      BlueskyStatus.Text = UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyConnecting);
      await blueskyCoordinator.StartAsync(BlueskyHandleEntry.Text).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or VouchaApiException)
    {
      BlueskyStatus.Text = UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyLinkFailed);
    }
    await RefreshBlueskyAsync().ConfigureAwait(true);
  }

  private async void OnCancelBlueskyClicked(object? sender, EventArgs e)
  {
    await blueskyCoordinator.CancelAsync().ConfigureAwait(true);
    await RefreshBlueskyAsync().ConfigureAwait(true);
  }

  private async void OnDisconnectBlueskyClicked(object? sender, EventArgs e)
  {
    var confirmed = await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyDisconnectConfirmation),
        UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyDisconnectConfirmation),
        UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyDisconnect),
        UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyCancel));
    if (!confirmed) return;
    try
    {
      BlueskyStatus.Text = UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyDisconnecting);
      await blueskyCoordinator.DisconnectAsync().ConfigureAwait(true);
      BlueskyStatus.Text = string.Empty;
    }
    catch (Exception ex) when (ex is HttpRequestException or VouchaApiException)
    {
      BlueskyStatus.Text = UiCopy.Localize(UiMessageKey.NativeSwiftSettingsBlueskyDisconnectFailed);
    }
    await RefreshBlueskyAsync().ConfigureAwait(true);
  }

  private void ApplyBlueskyStateCopy()
  {
    var key = blueskyCoordinator.State switch
    {
      NativeBlueskyLinkState.Connected => UiMessageKey.NativeSwiftSettingsBlueskyConnected,
      NativeBlueskyLinkState.Cancelled => UiMessageKey.NativeSwiftSettingsBlueskyCancelled,
      NativeBlueskyLinkState.Expired => UiMessageKey.NativeSwiftSettingsBlueskyExpired,
      NativeBlueskyLinkState.Finalizing => UiMessageKey.NativeSwiftSettingsBlueskyFinalizing,
      NativeBlueskyLinkState.Failed => UiMessageKey.NativeSwiftSettingsBlueskyLinkFailed,
      _ => default,
    };
    if (key != default) BlueskyStatus.Text = UiCopy.Localize(key);
  }
}

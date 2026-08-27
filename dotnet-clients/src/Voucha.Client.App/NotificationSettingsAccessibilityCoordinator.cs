using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public interface INotificationSettingsAccessibilityCoordinator
{
  void Activate(VisualElement heading);
  void Deactivate();
}

public interface INotificationAccessibilityPlatform
{
  bool Focus(VisualElement heading);
  void Announce(string text);
}

public interface INativeSemanticFocus
{
  bool MoveTo(VisualElement heading);
}

public interface INotificationAccessibilityRetryScheduler
{
  void Schedule(VisualElement heading, Action retry);
}

public sealed class MauiNotificationAccessibilityRetryScheduler : INotificationAccessibilityRetryScheduler
{
  public void Schedule(VisualElement heading, Action retry)
  {
    var retried = false;
    EventHandler? retryWhenReady = null;
    retryWhenReady = (_, _) => RetryOnce();
    heading.Loaded += retryWhenReady;
    heading.SizeChanged += retryWhenReady;
    heading.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), RetryOnce);

    void RetryOnce()
    {
      if (retried) return;
      retried = true;
      heading.Loaded -= retryWhenReady;
      heading.SizeChanged -= retryWhenReady;
      retry();
    }
  }
}

public sealed class MauiNativeSemanticFocus : INativeSemanticFocus
{
  public bool MoveTo(VisualElement heading)
  {
#if MACCATALYST
    if (heading.Handler?.PlatformView is UIKit.UIView nativeView)
    {
      UIKit.UIAccessibility.PostNotification(
          UIKit.UIAccessibilityPostNotification.ScreenChanged,
          nativeView);
      return true;
    }
    return false;
#elif WINDOWS
    if (heading.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeView)
    {
      var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer
          .CreatePeerForElement(nativeView);
      if (peer is null) return false;
      peer.RaiseAutomationEvent(
          Microsoft.UI.Xaml.Automation.Peers.AutomationEvents.AutomationFocusChanged);
      return true;
    }
    return false;
#else
    if (heading.Handler is null) return false;
    heading.SetSemanticFocus();
    return true;
#endif
  }
}

public sealed class MauiNotificationAccessibilityPlatform(INativeSemanticFocus semanticFocus)
    : INotificationAccessibilityPlatform
{
  public bool Focus(VisualElement heading)
  {
    return semanticFocus.MoveTo(heading);
  }

  public void Announce(string text) => SemanticScreenReader.Default.Announce(text);
}

public sealed class NotificationSettingsAccessibilityCoordinator(
    IUiLocalization localization,
    INotificationAccessibilityPlatform platform,
    INotificationAccessibilityRetryScheduler retryScheduler)
    : INotificationSettingsAccessibilityCoordinator
{
  private bool activated;
  private bool fallbackRetryAttempted;
  private VisualElement? pendingHeading;

  public void Activate(VisualElement heading)
  {
    if (activated) return;
    if (!ReferenceEquals(pendingHeading, heading)) Deactivate();
    pendingHeading = heading;
    heading.HandlerChanged -= OnHeadingHandlerChanged;
    heading.HandlerChanged += OnHeadingHandlerChanged;
    if (!platform.Focus(heading))
    {
      ScheduleFallbackRetry(heading);
      return;
    }
    activated = true;
    fallbackRetryAttempted = false;
    heading.HandlerChanged -= OnHeadingHandlerChanged;
    pendingHeading = null;
    platform.Announce(
        localization.Localize(UiMessageKey.NativeDotnetSettingsNotificationSettingsOpened));
  }

  public void Deactivate()
  {
    activated = false;
    fallbackRetryAttempted = false;
    if (pendingHeading is not null) pendingHeading.HandlerChanged -= OnHeadingHandlerChanged;
    pendingHeading = null;
  }

  private void OnHeadingHandlerChanged(object? sender, EventArgs e)
  {
    if (pendingHeading is not null) Activate(pendingHeading);
  }

  private void ScheduleFallbackRetry(VisualElement heading)
  {
    if (fallbackRetryAttempted) return;
    fallbackRetryAttempted = true;
    retryScheduler.Schedule(heading, () =>
    {
      if (!activated && ReferenceEquals(pendingHeading, heading)) Activate(heading);
    });
  }
}

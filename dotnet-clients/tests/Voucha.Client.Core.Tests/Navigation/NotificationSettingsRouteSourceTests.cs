using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class NotificationSettingsRouteSourceTests
{
  [Fact]
  public void MauiSettingsPageFocusesNotificationPreferencesForTheCanonicalRoute()
  {
    var intentPages = Source("AppShell.IntentPages.cs");
    var settingsIntentPages = Source("AppShell.SettingsIntentPages.cs");
    var xaml = Source("Pages", "SettingsPage.xaml");
    var code = Source("Pages", "SettingsPage.NotificationFocus.cs");

    Assert.Contains("CreateSettingsPage(match)", intentPages, StringComparison.Ordinal);
    Assert.Contains("ApplyInitialRouteMatch(match)", settingsIntentPages, StringComparison.Ordinal);
    Assert.Contains("\"/my/notification-settings\"", code, StringComparison.Ordinal);
    Assert.Contains("x:Name=\"NotificationPreferencesSection\"", xaml, StringComparison.Ordinal);
    Assert.Contains("pages:NotificationPreferencesView", xaml, StringComparison.Ordinal);
    Assert.Contains("IdentitySection.IsVisible = !notificationSettingsFocused", code, StringComparison.Ordinal);
    Assert.Contains("PrivacySection.IsVisible = !notificationSettingsFocused", code, StringComparison.Ordinal);
    Assert.Contains("SettingsScroll.ScrollToAsync(NotificationPreferencesSection", code, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiNotificationRouteAwaitsLoadingAndOffersLocalizedRetryAfterFailures()
  {
    var intentPages = Source("AppShell.IntentPages.cs");
    var settingsIntentPages = Source("AppShell.SettingsIntentPages.cs");
    var accountData = Source("Pages", "SettingsPage.AccountData.cs");
    var code = Source("Pages", "SettingsPage.NotificationFocus.cs");
    var initialLoad = Source("Pages", "SettingsPageInitialLoad.cs");

    Assert.Contains("TryApplySettingsRouteMatchAsync(match)", intentPages, StringComparison.Ordinal);
    Assert.Contains("await settingsPage.ApplyRouteMatchAsync(match)", settingsIntentPages, StringComparison.Ordinal);
    Assert.Contains("await SettingsPageInitialLoad.RunAsync(", accountData, StringComparison.Ordinal);
    Assert.Contains("LoadNotificationPreferencesForFocusedRouteAsync,", accountData, StringComparison.Ordinal);
    Assert.Contains(
        "() => Task.WhenAll(viewModel.LoadAsync(), emailAddressViewModel.LoadAsync())",
        accountData,
        StringComparison.Ordinal);
    Assert.True(
        accountData.IndexOf("LoadNotificationPreferencesForFocusedRouteAsync,", StringComparison.Ordinal) <
        accountData.IndexOf("Task.WhenAll(viewModel.LoadAsync(), emailAddressViewModel.LoadAsync())", StringComparison.Ordinal));
    Assert.Contains("var focusedLoadTask = Start(focusedLoad);", initialLoad, StringComparison.Ordinal);
    Assert.Contains("var broadLoadTask = Start(broadLoad);", initialLoad, StringComparison.Ordinal);
    Assert.True(
        initialLoad.IndexOf("var focusedLoadTask = Start(focusedLoad);", StringComparison.Ordinal) <
        initialLoad.IndexOf("var broadLoadTask = Start(broadLoad);", StringComparison.Ordinal));
    Assert.Contains("await Task.WhenAll(focusedLoadTask, broadLoadTask)", initialLoad, StringComparison.Ordinal);
    Assert.Contains("await notificationPreferences.LoadAsync()", code, StringComparison.Ordinal);
    Assert.DoesNotContain("ContinueWith(", code, StringComparison.Ordinal);
    Assert.Contains("NotificationPreferencesSection.SynchronizeControls();", code, StringComparison.Ordinal);
    Assert.True(
        code.IndexOf("await notificationPreferences.LoadAsync()", StringComparison.Ordinal) <
        code.IndexOf("NotificationPreferencesSection.SynchronizeControls();", StringComparison.Ordinal));
    Assert.Contains("DisplayAlertAsync(", code, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeCommonRetry", code, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.CommonCancel", code, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiNotificationRouteDefersInitialLoadingUntilThePageIsVisible()
  {
    var accountData = Source("Pages", "SettingsPage.AccountData.cs");
    var code = Source("Pages", "SettingsPage.NotificationFocus.cs");

    Assert.Contains("isPageVisible = true;", accountData, StringComparison.Ordinal);
    Assert.Contains("await SettingsPageInitialLoad.RunAsync(", accountData, StringComparison.Ordinal);
    Assert.True(
        accountData.IndexOf("isPageVisible = true;", StringComparison.Ordinal) <
        accountData.IndexOf("await SettingsPageInitialLoad.RunAsync(", StringComparison.Ordinal));
    Assert.Contains("return isPageVisible", code, StringComparison.Ordinal);
    Assert.Contains("Task.CompletedTask", code, StringComparison.Ordinal);
    Assert.Contains("!notificationSettingsFocused || !isPageVisible", code, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiNotificationRouteLoadsVisibleReapplicationsOnceAndStopsAfterCancel()
  {
    var settingsIntentPages = Source("AppShell.SettingsIntentPages.cs");
    var code = Source("Pages", "SettingsPage.NotificationFocus.cs");

    Assert.Contains("await settingsPage.ApplyRouteMatchAsync(match)", settingsIntentPages, StringComparison.Ordinal);
    Assert.Contains("while (notificationSettingsFocused && isPageVisible)", code, StringComparison.Ordinal);
    Assert.Contains("!retry) return false;", code, StringComparison.Ordinal);
    Assert.Equal(1, Count(code, "await notificationPreferences.LoadAsync()"));
  }

  [Fact]
  public void MauiNotificationRouteDoesNotPromptAfterThePageBecomesHiddenOrUnfocused()
  {
    var code = Source("Pages", "SettingsPage.NotificationFocus.cs");
    var catchIndex = code.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
    var alertIndex = code.IndexOf("DisplayAlertAsync(", StringComparison.Ordinal);
    var firstGuardIndex = code.IndexOf(
        "if (!notificationSettingsFocused || !isPageVisible) return false;",
        catchIndex,
        StringComparison.Ordinal);

    Assert.True(catchIndex >= 0);
    Assert.True(firstGuardIndex > catchIndex && firstGuardIndex < alertIndex);
    Assert.Contains(
        "if (!notificationSettingsFocused || !isPageVisible || !retry) return false;",
        code,
        StringComparison.Ordinal);
  }

  [Fact]
  public void MauiNotificationRouteSharesTheLoadAndPreservesEachCurrentRouteActivation()
  {
    var code = Source("Pages", "SettingsPage.NotificationFocus.cs");

    Assert.Contains("private Task<bool>? notificationPreferencesLoadTask;", code, StringComparison.Ordinal);
    Assert.Contains("private readonly NotificationSettingsRouteEpoch notificationRouteEpoch = new();", code, StringComparison.Ordinal);
    Assert.Contains("notificationRouteEpoch.Apply(notificationRouteApplied);", code, StringComparison.Ordinal);
    Assert.Contains("var routeEpoch = notificationRouteEpoch.Value;", code, StringComparison.Ordinal);
    Assert.Contains("if (loadTask is null || loadTask.IsCompleted)", code, StringComparison.Ordinal);
    Assert.Contains("var loaded = await loadTask.ConfigureAwait(true);", code, StringComparison.Ordinal);
    Assert.Contains("routeEpoch != notificationRouteEpoch.Value", code, StringComparison.Ordinal);
    Assert.Equal(1, Count(code, "NotificationPreferencesSection.SynchronizeControls();"));
    Assert.Equal(1, Count(code, "notificationAccessibility.Activate("));
    Assert.Contains("notificationAccessibility.Deactivate();", code, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiNotificationRouteSkipsQueuedAccessibilityActivationAfterDeparture()
  {
    var code = Source("Pages", "SettingsPage.NotificationFocus.cs");
    var dispatchIndex = code.IndexOf("Dispatcher.Dispatch(() =>", StringComparison.Ordinal);
    var guardIndex = code.IndexOf(
        "if (routeEpoch != notificationRouteEpoch.Value || !notificationSettingsFocused || !isPageVisible) return;",
        dispatchIndex,
        StringComparison.Ordinal);
    var activationIndex = code.IndexOf("notificationAccessibility.Activate(", dispatchIndex, StringComparison.Ordinal);

    Assert.True(dispatchIndex >= 0);
    Assert.True(guardIndex > dispatchIndex && guardIndex < activationIndex);
  }

  [Fact]
  public void MauiNotificationAccessibilityUsesPlatformSemanticFocusAndBoundedReadinessRetry()
  {
    var coordinator = Source("NotificationSettingsAccessibilityCoordinator.cs");

    Assert.Contains("#elif WINDOWS", coordinator, StringComparison.Ordinal);
    Assert.Contains("FrameworkElementAutomationPeer", coordinator, StringComparison.Ordinal);
    Assert.Contains("CreatePeerForElement(nativeView)", coordinator, StringComparison.Ordinal);
    Assert.Contains("if (peer is null) return false;", coordinator, StringComparison.Ordinal);
    Assert.Contains("AutomationEvents.AutomationFocusChanged", coordinator, StringComparison.Ordinal);
    Assert.Contains("#else", coordinator, StringComparison.Ordinal);
    Assert.Contains("heading.SetSemanticFocus();", coordinator, StringComparison.Ordinal);
    Assert.True(
        coordinator.IndexOf("AutomationEvents.AutomationFocusChanged", StringComparison.Ordinal) <
        coordinator.IndexOf("heading.SetSemanticFocus();", StringComparison.Ordinal));
    Assert.DoesNotContain("FocusState.Programmatic", coordinator, StringComparison.Ordinal);
    Assert.Contains("heading.Loaded += retryWhenReady;", coordinator, StringComparison.Ordinal);
    Assert.Contains("heading.SizeChanged += retryWhenReady;", coordinator, StringComparison.Ordinal);
    Assert.Contains("heading.Dispatcher.DispatchDelayed", coordinator, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiNotificationRouteInvalidatesAndDeactivatesWhenThePageDisappears()
  {
    var accountData = Source("Pages", "SettingsPage.AccountData.cs");

    Assert.Contains("notificationRouteEpoch.Invalidate();", accountData, StringComparison.Ordinal);
    Assert.Contains("notificationAccessibility.Deactivate();", accountData, StringComparison.Ordinal);
    Assert.False(File.Exists(ExistingAppSourcePath("Pages", "SettingsPage.NotificationAccessibilityLifecycle.cs")));
  }

  [Fact]
  public void InboxSettingsActionUsesTheAppShellPathBoundaryAndCatchesEventFailures()
  {
    var action = Source("NotificationSettingsInboxAction.cs");
    var notifications = Source("Pages", "NotificationsPage.xaml.cs");

    Assert.Contains("ArgumentNullException.ThrowIfNull(appShell)", action, StringComparison.Ordinal);
    Assert.Contains("appShell.OpenNativePathAsync(SettingsRoute)", action, StringComparison.Ordinal);
    Assert.DoesNotContain("AppLinkDispatcher.DispatchAsync", action, StringComparison.Ordinal);
    Assert.Contains("private async void OnNotificationSettingsClicked", notifications, StringComparison.Ordinal);
    Assert.Contains("await NotificationSettingsInboxAction.OpenAsync(appShell);", notifications, StringComparison.Ordinal);
    Assert.Contains("catch (Exception ex)", notifications, StringComparison.Ordinal);
    Assert.Contains("LogNotificationSettingsNavigationFailed(logger, ex)", notifications, StringComparison.Ordinal);
  }

  private static int Count(string value, string fragment) =>
      value.Split(fragment, StringSplitOptions.None).Length - 1;

  private static string Source(params string[] parts)
  {
    foreach (var root in RootCandidates())
    {
      var candidate = AppSourcePath(root, parts);
      if (File.Exists(candidate)) return File.ReadAllText(candidate);
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }

  private static string ExistingAppSourcePath(params string[] parts) =>
      RootCandidates()
          .Select(root => AppSourcePath(root, parts))
          .First(path => Directory.Exists(Path.GetDirectoryName(path)));

  private static string AppSourcePath(string root, params string[] parts) =>
      Path.Combine(new[] { root, "dotnet-clients", "src", "Voucha.Client.App" }.Concat(parts).ToArray());

  private static IEnumerable<string> RootCandidates()
  {
    foreach (var start in new[] { ThisFileDirectory(), Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      if (string.IsNullOrEmpty(start)) continue;

      var directory = new DirectoryInfo(start);
      while (directory is not null)
      {
        yield return directory.FullName;
        directory = directory.Parent;
      }
    }
  }

  private static string ThisFileDirectory()
  {
    var sourceFile = ThisFile();
    return Path.GetDirectoryName(sourceFile) ?? "";
  }

  private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}

using System.Globalization;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class NotificationPreferencesViewTests
{
  [Fact]
  public void AccessibilityActivationRetriesAfterFailedProgrammaticFocusWithoutHandlerChange()
  {
    var platform = new RecordingAccessibilityPlatform(false, true);
    var retryScheduler = new RecordingAccessibilityRetryScheduler();
    var coordinator = new NotificationSettingsAccessibilityCoordinator(UiLocalization.English, platform, retryScheduler);

    coordinator.Activate(new Label());

    Assert.Equal(1, platform.FocusCount);
    retryScheduler.Run();
    Assert.Equal(2, platform.FocusCount);
    Assert.Equal(["Notification settings opened"], platform.Announcements);
  }

  [Fact]
  public async Task NotificationControlsHaveLocalizedSemanticNames()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["native.dotnet.settings.engagementEmails"] = "Engagement emails",
        ["native.dotnet.settings.newsDigest"] = "News digest",
        ["native.dotnet.settings.moderationEmails"] = "Moderation emails",
        ["native.dotnet.settings.communityDigest"] = "Community digest",
        ["native.dotnet.settings.moderationEmailCadence"] = "Moderation email cadence",
      },
    };
    var model = new NotificationPreferencesViewModel(new SettingsService());
    var view = new NotificationPreferencesView { BindingContext = model };

    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();

    Assert.Equal("Engagement emails", SemanticProperties.GetDescription(Find<Switch>(view, "notification-engagement")));
    Assert.Equal("News digest", SemanticProperties.GetDescription(Find<Picker>(view, "notification-news-digest")));
    Assert.Equal("Moderation emails", SemanticProperties.GetDescription(Find<Switch>(view, "notification-moderation")));
    Assert.Equal("Community digest", SemanticProperties.GetDescription(Find<Picker>(view, "notification-community-digest")));
    Assert.Equal("Moderation email cadence", SemanticProperties.GetDescription(Find<Picker>(view, "notification-cadence")));
  }

  [Fact]
  public async Task WeekdaySemanticDescriptionsReflectSelectionAndLocale()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var model = new NotificationPreferencesViewModel(new SettingsService(), localization: localization, localeController: controller);
    var view = new NotificationPreferencesView { BindingContext = model };
    view.ConfigureLocalization(localization, controller);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();
    AssertWeekdayDescription(view, localization, 1, selected: true);
    AssertWeekdayDescription(view, localization, 2, selected: false);

    controller.ApplySavedLocale("es");

    AssertWeekdayDescription(view, localization, 1, selected: true);
    AssertWeekdayDescription(view, localization, 2, selected: false);
  }

  private static void AssertWeekdayDescription(
      NotificationPreferencesView view,
      IUiLocalization localization,
      int day,
      bool selected)
  {
    var dayName = localization.Culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(day % 7));
    var key = selected
        ? UiMessageKey.NativeDotnetSettingsModerationEmailDaySelected
        : UiMessageKey.NativeDotnetSettingsModerationEmailDayUnselected;

    Assert.Equal(localization.Format(key, ("day", dayName)), SemanticProperties.GetDescription(DayButton(view, day)));
  }

  private sealed class RecordingAccessibilityRetryScheduler : INotificationAccessibilityRetryScheduler
  {
    private Action? retry;
    public void Schedule(VisualElement heading, Action retry) => this.retry = retry;
    public void Run() => Assert.IsType<Action>(retry).Invoke();
  }
}

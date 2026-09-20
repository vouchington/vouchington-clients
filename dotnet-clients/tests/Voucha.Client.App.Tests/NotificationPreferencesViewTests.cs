using System.Collections;
using System.Globalization;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed partial class NotificationPreferencesViewTests
{
  [Fact]
  public async Task AttachedViewRendersExplicitNotificationControls()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
        ["native.dotnet.settings.moderationEmailTime"] = "Moderation email time",
        ["native.dotnet.settings.moderationEmailTimezone"] = "Moderation email timezone",
      },
    };
    var viewModel = new NotificationPreferencesViewModel(new SettingsService());
    var view = new NotificationPreferencesView { BindingContext = viewModel };
    var page = new ContentPage { Content = view };
    page.Measure(1024, 768);
    page.Arrange(new Rect(0, 0, 1024, 768));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();

    Assert.NotNull(Find<Label>(view, "notification-preferences-heading"));
    Assert.NotNull(Find<Switch>(view, "notification-engagement"));
    Assert.NotNull(Find<Switch>(view, "notification-moderation"));
    Assert.NotNull(Find<Picker>(view, "notification-news-digest"));
    Assert.NotNull(Find<Picker>(view, "notification-community-digest"));
    Assert.NotNull(Find<Picker>(view, "notification-cadence"));
    var timePicker = Find<TimePicker>(view, "notification-time");
    var timezonePicker = Find<Picker>(view, "notification-timezone");
    Assert.Equal("Moderation email time", Find<Label>(view, "notification-time-label").Text);
    Assert.Equal("Moderation email timezone", Find<Label>(view, "notification-timezone-label").Text);
    Assert.Equal("Moderation email time", SemanticProperties.GetDescription(timePicker));
    Assert.Equal("Moderation email timezone", SemanticProperties.GetDescription(timezonePicker));
  }

  [Fact]
  public async Task SynchronizeControlsDoesNotEmitPreferenceMutations()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    var model = new NotificationPreferencesViewModel(new SettingsService());
    var view = new NotificationPreferencesView { BindingContext = model };
    var emitted = 0;
    view.PreferenceChanged += (_, _) => emitted++;
    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();
    Assert.Equal(0, emitted);
  }

  [Fact]
  public async Task SynchronizeControlsIncludesTheSavedTimezoneAlias()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    var service = new SettingsService
    {
      Preferences = new(true, "weekly", true, "weekly", "selected_days", [1], "09:00", "US/Pacific"),
    };
    var model = new NotificationPreferencesViewModel(service, new NoTimezoneResolver());
    var view = new NotificationPreferencesView { BindingContext = model };

    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();

    var timezonePicker = Find<Picker>(view, "notification-timezone");
    Assert.Contains("US/Pacific", timezonePicker.ItemsSource.Cast<string>());
    Assert.Equal("US/Pacific", timezonePicker.SelectedItem);
  }

  [Fact]
  public async Task WeekdayLabelsUseTheUiLocaleAndRefreshWhenItChanges()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var view = new NotificationPreferencesView { BindingContext = new NotificationPreferencesViewModel(new SettingsService()) };
    view.ConfigureLocalization(new UiLocalization(controller), controller);

    await ((NotificationPreferencesViewModel)view.BindingContext).LoadAsync(TestContext.Current.CancellationToken);
    controller.ApplySavedLocale("es");
    view.SynchronizeControls();
    Assert.Equal(CultureInfo.GetCultureInfo("es").DateTimeFormat.GetAbbreviatedDayName(DayOfWeek.Monday), DayButton(view, 1).Text);

    controller.ApplySavedLocale("fr");
    Assert.Equal(CultureInfo.GetCultureInfo("fr").DateTimeFormat.GetAbbreviatedDayName(DayOfWeek.Monday), DayButton(view, 1).Text);
  }

  [Fact]
  public async Task PickerLabelsRefreshWhenTheUiLocaleChanges()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var model = new NotificationPreferencesViewModel(
        new SettingsService(),
        localization: localization,
        localeController: controller);
    var view = new NotificationPreferencesView { BindingContext = model };
    view.ConfigureLocalization(localization, controller);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();
    var newsPicker = Find<Picker>(view, "notification-news-digest");
    var communityPicker = Find<Picker>(view, "notification-community-digest");
    var cadencePicker = Find<Picker>(view, "notification-cadence");
    var digestOptions = model.DigestOptions;
    var cadenceOptions = model.CadenceOptions;

    controller.ApplySavedLocale("es");

    Assert.NotSame(digestOptions, model.DigestOptions);
    Assert.NotSame(cadenceOptions, model.CadenceOptions);
    Assert.Same(model.DigestOptions, newsPicker.ItemsSource);
    Assert.Same(model.DigestOptions, communityPicker.ItemsSource);
    Assert.Same(model.CadenceOptions, cadencePicker.ItemsSource);
    Assert.Equal(
        localization.Localize(UiMessageKey.NativeSwiftSettingsWeekly),
        ((SettingsOptionViewModel)newsPicker.SelectedItem).DisplayLabel);
    Assert.Equal(
        localization.Localize(UiMessageKey.NativeSwiftSettingsSelectedDays),
        ((SettingsOptionViewModel)cadencePicker.SelectedItem).DisplayLabel);
  }

  [Fact]
  public async Task AccessibleSwitchChangesEmitMutationsWithoutKeyboardFocus()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    var model = new NotificationPreferencesViewModel(new SettingsService());
    var view = new NotificationPreferencesView { BindingContext = model };
    var changes = new List<NotificationPreferenceChangedEventArgs>();
    view.PreferenceChanged += (_, change) => changes.Add(change);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();
    var engagement = Find<Switch>(view, "notification-engagement");
    var moderation = Find<Switch>(view, "notification-moderation");
    Assert.False(engagement.IsFocused);
    Assert.False(moderation.IsFocused);

    engagement.IsToggled = false;
    moderation.IsToggled = false;

    Assert.Equal(["engagement", "moderation"], changes.Select(change => change.Field));
    Assert.All(changes, change => Assert.False((bool)change.Value!));
  }

  [Fact]
  public async Task PendingDaysMutationImmediatelyDisablesDayControlsAndKeepsTheFinalDayDisabled()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    var service = new SettingsService();
    var model = new NotificationPreferencesViewModel(service);
    var view = new NotificationPreferencesView { BindingContext = model };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();
    var days = view.FindByName<HorizontalStackLayout>("DaysControls").Children.OfType<Button>().ToArray();
    Assert.Contains(days, button => !button.IsEnabled);
    service.PendingUpdate = new TaskCompletionSource<EmailPreferencesResponse>();
    var update = model.SetDaysAsync([1, 3, 5], TestContext.Current.CancellationToken);
    Assert.All(days, button => Assert.False(button.IsEnabled));
    service.PendingUpdate.SetResult(new EmailPreferencesResponse(new(true, "weekly", true, "weekly", "selected_days", [1, 3, 5], "09:00", null)));
    await update;
  }

  [Theory]
  [InlineData("news")]
  [InlineData("community")]
  [InlineData("cadence")]
  [InlineData("time")]
  [InlineData("timezone")]
  public async Task FailedMutationResynchronizesEveryRenderedPreferenceControl(string field)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    var service = new SettingsService();
    var model = new NotificationPreferencesViewModel(service, new NoTimezoneResolver());
    var view = new NotificationPreferencesView { BindingContext = model };
    await model.LoadAsync(TestContext.Current.CancellationToken);
    view.SynchronizeControls();
    service.PendingUpdate = new TaskCompletionSource<EmailPreferencesResponse>();

    var update = field switch
    {
      "news" => model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken),
      "community" => model.SetCommunityDigestAsync("none", TestContext.Current.CancellationToken),
      "cadence" => model.SetCadenceAsync("daily", TestContext.Current.CancellationToken),
      "time" => model.SetTimeAsync(new TimeSpan(15, 0, 0), TestContext.Current.CancellationToken),
      _ => model.SetTimezoneAsync("UTC", TestContext.Current.CancellationToken),
    };
    service.PendingUpdate.SetException(new InvalidOperationException("rejected"));

    await Assert.ThrowsAsync<InvalidOperationException>(() => update);

    AssertRestoredControl(view, field);
    Assert.True(model.HasErrorMessage);
    Assert.Equal("We could not save your notification setting. Try again.", model.ErrorMessage);
    Assert.True(Find<Label>(view, "notification-preferences-error").IsVisible);
  }

  [Fact]
  public void AccessibilityAnnouncementOccursOncePerRouteApplicationAndRearmsForAReusedPage()
  {
    var platform = new RecordingAccessibilityPlatform();
    var heading = new Label();
    var coordinator = new NotificationSettingsAccessibilityCoordinator(UiLocalization.English, platform, new RecordingAccessibilityRetryScheduler());
    coordinator.Activate(heading);
    coordinator.Activate(heading);
    coordinator.Deactivate();
    coordinator.Activate(heading);
    Assert.Equal(2, platform.FocusCount);
    Assert.Equal(["Notification settings opened", "Notification settings opened"], platform.Announcements);
  }

  [Fact]
  public void AccessibilityActivationRetriesAfterAttachmentWithoutAnEarlyAnnouncement()
  {
    var platform = new RecordingAccessibilityPlatform(false, true);
    var coordinator = new NotificationSettingsAccessibilityCoordinator(UiLocalization.English, platform, new RecordingAccessibilityRetryScheduler());
    var heading = new Label();

    coordinator.Activate(heading);
    coordinator.Activate(heading);

    Assert.Equal(2, platform.FocusCount);
    Assert.Equal(["Notification settings opened"], platform.Announcements);
  }

  [Fact]
  public void AccessibilityActivationDoesNotAnnounceWhenNativeFocusFails()
  {
    var platform = new RecordingAccessibilityPlatform(false);
    var coordinator = new NotificationSettingsAccessibilityCoordinator(UiLocalization.English, platform, new RecordingAccessibilityRetryScheduler());

    coordinator.Activate(new Label());

    Assert.Empty(platform.Announcements);
  }

  [Fact]
  public void MauiAccessibilityPlatformUsesTheNativeSemanticFocusAdapter()
  {
    var semanticFocus = new RecordingSemanticFocus();
    var heading = new Label();

    new MauiNotificationAccessibilityPlatform(semanticFocus).Focus(heading);

    Assert.Same(heading, semanticFocus.Heading);
  }

  [Fact]
  public async Task NotificationInboxSettingsActionDispatchesTheCanonicalSettingsRoute()
  {
    var appShell = new AppShell();

    await NotificationSettingsInboxAction.OpenAsync(appShell);

    Assert.Equal("/my/notification-settings", appShell.LastOpenedPath);
  }

  [Fact]
  public async Task NotificationInboxSettingsActionRejectsAMissingAppShell()
  {
    await Assert.ThrowsAsync<ArgumentNullException>(() => NotificationSettingsInboxAction.OpenAsync(null!));
  }

  [Theory]
  [InlineData(true, false, true)]
  [InlineData(true, true, false)]
  [InlineData(false, false, false)]
  [InlineData(false, true, false)]
  public void FocusedNotificationRouteKeepsBlueskyHiddenAcrossPresentationRefreshes(
      bool fediverseEnabled,
      bool notificationSettingsFocused,
      bool expectedVisible)
  {
    Assert.Equal(
        expectedVisible,
        SettingsRoutePresentation.ShowsBlueskySection(fediverseEnabled, notificationSettingsFocused));
  }

  [Fact]
  public async Task FocusedNotificationLoadStartsBeforeBroadAccountDataLoadsComplete()
  {
    var focusedStarted = new TaskCompletionSource();
    var focusedLoad = new TaskCompletionSource();
    var broadStarted = new TaskCompletionSource();
    var broadLoad = new TaskCompletionSource();

    var load = SettingsPageInitialLoad.RunAsync(
        () =>
        {
          focusedStarted.SetResult();
          return focusedLoad.Task;
        },
        () =>
        {
          broadStarted.SetResult();
          return broadLoad.Task;
        });

    await focusedStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(broadStarted.Task.IsCompleted);
    Assert.False(load.IsCompleted);
    broadLoad.SetResult();
    Assert.False(load.IsCompleted);
    focusedLoad.SetResult();
    await load;
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>().Single(element => element.AutomationId == automationId);

  private static Button DayButton(NotificationPreferencesView view, int day) =>
      view.FindByName<HorizontalStackLayout>("DaysControls").Children.OfType<Button>()
          .Single(button => button.CommandParameter is string parameter && parameter == day.ToString(CultureInfo.InvariantCulture));

  private static void AssertRestoredControl(NotificationPreferencesView view, string field)
  {
    switch (field)
    {
      case "news": Assert.Equal("weekly", ((SettingsOptionViewModel)Find<Picker>(view, "notification-news-digest").SelectedItem).Value); break;
      case "community": Assert.Equal("weekly", ((SettingsOptionViewModel)Find<Picker>(view, "notification-community-digest").SelectedItem).Value); break;
      case "cadence": Assert.Equal("selected_days", ((SettingsOptionViewModel)Find<Picker>(view, "notification-cadence").SelectedItem).Value); break;
      case "time": Assert.Equal(new TimeSpan(9, 0, 0), Find<TimePicker>(view, "notification-time").Time); break;
      default: Assert.Equal("America/Los_Angeles", Find<Picker>(view, "notification-timezone").SelectedItem); break;
    }
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class RecordingAccessibilityPlatform : INotificationAccessibilityPlatform
  {
    private readonly Queue<bool> focusResults;
    public RecordingAccessibilityPlatform(params bool[] focusResults) => this.focusResults = new(focusResults);
    public int FocusCount { get; private set; }
    public List<string> Announcements { get; } = [];
    public bool Focus(VisualElement heading)
    {
      FocusCount++;
      return focusResults.Count == 0 || focusResults.Dequeue();
    }
    public void Announce(string text) => Announcements.Add(text);
  }

  private sealed class RecordingSemanticFocus : INativeSemanticFocus
  {
    public VisualElement? Heading { get; private set; }
    public bool MoveTo(VisualElement heading)
    {
      Heading = heading;
      return true;
    }
  }

  private sealed class NoTimezoneResolver : INotificationTimezoneResolver
  {
    public string? ResolveIanaTimezone() => null;
  }

  private sealed record StubLanguageProvider(string Language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => [Language];
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class SettingsService : ISettingsService, INotificationPreferencesService
  {
    public TaskCompletionSource<EmailPreferencesResponse>? PendingUpdate { get; set; }
    public EmailPreferences Preferences { get; set; } = new(true, "weekly", true, "weekly", "selected_days", [1], "09:00", null);

    public Task<EmailPreferencesResponse> FetchEmailPreferencesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new EmailPreferencesResponse(Preferences));

    public Task<EmailPreferencesResponse> UpdateEmailPreferencesAsync(
        UpdateEmailPreferencesBody body,
        CancellationToken cancellationToken = default) =>
        PendingUpdate?.Task ?? FetchEmailPreferencesAsync(cancellationToken);
    public Task<MyIdentityResponse> FetchMyIdentityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyIdentityResponse(new User("user-1", "alice")));

    public Task<MyIdentityResponse> UpdateMyIdentityAsync(
        UpdateMyIdentityBody body, CancellationToken cancellationToken = default) =>
        FetchMyIdentityAsync(cancellationToken);

    public Task<MyProfileResponse> FetchMyProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MyProfileResponse(new MyProfile("profile-1", "")));

    public Task<MyProfileResponse> UpdateMyProfileAsync(
        string markdown, CancellationToken cancellationToken = default) => FetchMyProfileAsync(cancellationToken);

    public Task<AuthSessionListResponse> FetchAuthSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthSessionListResponse([], new PageInfo(null, false, null)));

    public Task DeleteAuthSessionAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RevokeAuthSessionsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<UserResponse> FetchUserAsync(
        string idOrSlug, bool includeBio = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserResponse(new User(
            "user-1", "alice", EngagementEmailsEnabled: true, ModerationEmailsEnabled: true)));

    public Task<UserResponse> UpdateUserAsync(
        string idOrSlug, UpdateUserPrivacyBody body, CancellationToken cancellationToken = default) =>
        FetchUserAsync(idOrSlug, cancellationToken: cancellationToken);

    public Task<UserDataRequestResponse?> FetchUserDataRequestAsync(
        string idOrSlug, CancellationToken cancellationToken = default) =>
        Task.FromResult<UserDataRequestResponse?>(null);

    public Task<UserDataRequestCreationResponse> CreateUserDataRequestAsync(
        string idOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<DeleteUserResponse> DeleteUserAsync(
        string idOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ApiKeyListResponse> FetchApiKeysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApiKeyListResponse([], new PageInfo(null, false, null)));

    public Task<ApiKeyCreationResponse> CreateApiKeyAsync(
        string label, string type, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteApiKeyAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<ProfileLinkListResponse> FetchProfileLinksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProfileLinkListResponse([], new PageInfo(null, false, null)));

    public Task<ProfileLinkResponse> CreateProfileLinkAsync(
        CreateProfileLinkBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ProfileLinkListResponse> ReorderProfileLinksAsync(
        ReorderProfileLinksBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ProfileLinkResponse> UpdateProfileLinkAsync(
        string id, UpdateProfileLinkBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteProfileLinkAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<MembershipResponse?> FetchMembershipAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<MembershipResponse?>(null);

    public Task<MembershipPlansResponse> FetchMembershipPlansAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MembershipPlansResponse([]));

    public Task<CheckoutSessionResponse> CreateMembershipCheckoutSessionAsync(
        MembershipCheckoutBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PortalSessionResponse> CreateMembershipPortalSessionAsync(
        MembershipPortalBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task CancelMembershipAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<WebPushSubscriptionListResponse> FetchPushSubscriptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebPushSubscriptionListResponse([], new PageInfo(null, false, null)));

    public Task DeletePushSubscriptionAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}

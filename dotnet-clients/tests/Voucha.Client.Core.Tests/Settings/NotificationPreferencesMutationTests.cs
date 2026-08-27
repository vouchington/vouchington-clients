using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Theory]
  [MemberData(nameof(FieldMutations))]
  public async Task EachMutationSendsExactlyOnePreferenceField(
      string field,
      Func<NotificationPreferencesViewModel, CancellationToken, Task> mutate)
  {
    var service = new DeferredEmailPreferencesService();
    var model = await LoadAsync(service);
    await mutate(model, TestContext.Current.CancellationToken);
    Assert.Single(service.Bodies);
    AssertOnly(service.Bodies[0], field);
  }

  [Fact]
  public async Task SameFieldMutationsAreSerializedWithoutDroppingTheLatestOptimisticValue()
  {
    var service = new DeferredEmailPreferencesService();
    var firstUpdate = service.EnqueueUpdate();
    var secondUpdate = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var first = model.SetDaysAsync([1, 3], TestContext.Current.CancellationToken);
    var secondStarted = service.EnqueueUpdateStarted();
    var second = model.SetDaysAsync([2, 4], TestContext.Current.CancellationToken);

    Assert.Equal([2, 4], model.ModerationEmailDaysOfWeek);
    Assert.False(model.IsDaysEnabled);
    Assert.Single(service.Bodies);
    Assert.Equal([1, 3], service.Bodies[0].ModerationEmailDaysOfWeek);

    firstUpdate.SetResult(new(service.Preferences with { ModerationEmailDaysOfWeek = [1, 3] }));
    await first;
    await secondStarted.Task;
    Assert.Equal([2, 4], model.ModerationEmailDaysOfWeek);
    Assert.Equal(2, service.Bodies.Count);
    Assert.Equal([2, 4], service.Bodies[1].ModerationEmailDaysOfWeek);

    secondUpdate.SetResult(new(service.Preferences with { ModerationEmailDaysOfWeek = [2, 4] }));
    await second;
    Assert.Equal([2, 4], model.ModerationEmailDaysOfWeek);
    Assert.True(model.IsDaysEnabled);
  }

  [Fact]
  public async Task FailedEarlierDayMutationDoesNotRollbackALaterOptimisticSelection()
  {
    var service = new DeferredEmailPreferencesService();
    var failedUpdate = service.EnqueueUpdate();
    var succeedingUpdate = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var failed = model.SetDaysAsync([1, 3], TestContext.Current.CancellationToken);
    var succeedingStarted = service.EnqueueUpdateStarted();
    var succeeding = model.SetDaysAsync([2, 4], TestContext.Current.CancellationToken);

    failedUpdate.SetException(new InvalidOperationException("nope"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => failed);
    await succeedingStarted.Task;
    Assert.Equal([2, 4], model.ModerationEmailDaysOfWeek);
    Assert.Equal(2, service.Bodies.Count);

    succeedingUpdate.SetResult(new(service.Preferences with { ModerationEmailDaysOfWeek = [2, 4] }));
    await succeeding;
    Assert.Equal([2, 4], model.ModerationEmailDaysOfWeek);
  }

  [Fact]
  public async Task FailedQueuedMutationDoesNotLeaveAnErrorAfterTheNewerSameFieldSaveSucceeds()
  {
    var service = new DeferredEmailPreferencesService();
    var failedUpdate = service.EnqueueUpdate();
    var succeedingUpdate = service.EnqueueUpdate();
    var secondStarted = service.EnqueueUpdateStarted();
    var model = await LoadAsync(service);
    var failed = model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    var succeeding = model.SetNewsDigestAsync("none", TestContext.Current.CancellationToken);

    failedUpdate.SetException(new InvalidOperationException("nope"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => failed);
    await secondStarted.Task;
    Assert.Null(model.ErrorMessage);

    succeedingUpdate.SetResult(new(service.Preferences with { NewsDigestFrequency = "none" }));
    await succeeding;
    Assert.Null(model.ErrorMessage);
  }

  [Fact]
  public async Task SaveErrorRefreshesWhenTheUiLocaleChanges()
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var service = new DeferredEmailPreferencesService();
    var failedUpdate = service.EnqueueUpdate();
    using var model = new NotificationPreferencesViewModel(
        service, new NoTimezoneResolver(), localization, controller);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    var mutation = model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    failedUpdate.SetException(new InvalidOperationException("nope"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => mutation);
    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetSettingsNotificationSettingsSaveError), model.ErrorMessage);

    var changed = new List<string?>();
    model.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);
    controller.ApplySavedLocale("es");
    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetSettingsNotificationSettingsSaveError), model.ErrorMessage);
    Assert.Contains(nameof(NotificationPreferencesViewModel.ErrorMessage), changed);
  }

  [Fact]
  public async Task InitialLoadFailureKeepsControlsDisabledUntilARetrySucceeds()
  {
    var service = new DeferredEmailPreferencesService();
    var model = new NotificationPreferencesViewModel(service, new NoTimezoneResolver());
    var failedFetch = service.EnqueueFetch();
    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    await failedFetch.Started.Task;
    failedFetch.Completion.SetException(new InvalidOperationException("unavailable"));

    await Assert.ThrowsAsync<InvalidOperationException>(() => load);
    Assert.False(model.HasLoadedPreferences);
    Assert.False(model.IsEngagementEnabled);
    Assert.False(model.IsNewsEnabled);
    Assert.False(model.IsTimezoneEnabled);

    var retryFetch = service.EnqueueFetch();
    var retry = model.LoadAsync(TestContext.Current.CancellationToken);
    await retryFetch.Started.Task;
    retryFetch.Completion.SetResult(new(service.Preferences));
    await retry;

    Assert.True(model.HasLoadedPreferences);
    Assert.True(model.IsNewsEnabled);
    await model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    Assert.Equal("daily", model.NewsDigestFrequency);
  }

  [Fact]
  public async Task FailedLatestDayMutationRollsBackToTheLastSavedSelection()
  {
    var service = new DeferredEmailPreferencesService();
    var firstUpdate = service.EnqueueUpdate();
    var failedUpdate = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var first = model.SetDaysAsync([1, 3], TestContext.Current.CancellationToken);
    var failed = model.SetDaysAsync([2, 4], TestContext.Current.CancellationToken);

    firstUpdate.SetResult(new(service.Preferences with { ModerationEmailDaysOfWeek = [1, 3] }));
    await first;
    failedUpdate.SetException(new InvalidOperationException("nope"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => failed);

    Assert.Equal([1, 3], model.ModerationEmailDaysOfWeek);
    Assert.True(model.IsDaysEnabled);
  }

  [Fact]
  public async Task CancelledQueuedDayMutationRestoresThePredecessorWithoutSendingItsRequest()
  {
    var service = new DeferredEmailPreferencesService();
    var firstUpdate = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var first = model.SetDaysAsync([1, 3], TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    var cancelled = model.SetDaysAsync([2, 4], cancellation.Token);

    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    Assert.Single(service.Bodies);
    Assert.Equal([1, 3], model.ModerationEmailDaysOfWeek);
    Assert.False(model.IsDaysEnabled);

    firstUpdate.SetResult(new(service.Preferences with { ModerationEmailDaysOfWeek = [1, 3] }));
    await first;
    Assert.Single(service.Bodies);
    Assert.Equal([1, 3], model.ModerationEmailDaysOfWeek);
    Assert.True(model.IsDaysEnabled);
  }

  [Fact]
  public async Task LateLoadCapturedDuringACancelledMutationCannotOverwriteItsSuccessfulPredecessor()
  {
    var service = new DeferredEmailPreferencesService();
    var firstUpdate = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var first = model.SetDaysAsync([1, 3], TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    var cancelled = model.SetDaysAsync([2, 4], cancellation.Token);
    var fetch = service.EnqueueFetch();
    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    await fetch.Started.Task;

    cancellation.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    firstUpdate.SetResult(new(service.Preferences with { ModerationEmailDaysOfWeek = [1, 3] }));
    await first;
    fetch.Completion.SetResult(new(service.Preferences with { ModerationEmailDaysOfWeek = [6, 7] }));
    await load;

    Assert.Equal([1, 3], model.ModerationEmailDaysOfWeek);
    await model.SetDaysAsync([4, 5], TestContext.Current.CancellationToken);
    Assert.Equal([4, 5], model.ModerationEmailDaysOfWeek);
    Assert.True(model.IsDaysEnabled);
  }

  [Fact]
  public async Task DifferentFieldsRemainConcurrentAndMergeReverseResponses()
  {
    var service = new DeferredEmailPreferencesService();
    var news = service.EnqueueUpdate();
    var community = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var first = model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    var second = model.SetCommunityDigestAsync("none", TestContext.Current.CancellationToken);
    Assert.Equal(2, service.Bodies.Count);
    community.SetResult(new(service.Preferences with { CommunityDigestFrequency = "none" }));
    await second;
    news.SetResult(new(service.Preferences with { NewsDigestFrequency = "daily", CommunityDigestFrequency = "weekly" }));
    await first;
    Assert.Equal("daily", model.NewsDigestFrequency);
    Assert.Equal("none", model.CommunityDigestFrequency);
  }

  [Fact]
  public async Task FailureRollsBackOnlyTheFailedField()
  {
    var service = new DeferredEmailPreferencesService();
    var failed = service.EnqueueUpdate();
    var community = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var first = model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    var second = model.SetCommunityDigestAsync("none", TestContext.Current.CancellationToken);
    community.SetResult(new(service.Preferences with { CommunityDigestFrequency = "none" }));
    await second;
    failed.SetException(new InvalidOperationException("nope"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => first);
    Assert.Equal("weekly", model.NewsDigestFrequency);
    Assert.Equal("none", model.CommunityDigestFrequency);
  }

  [Fact]
  public async Task NormalizedResponseCommitsOnlyItsRequestedField()
  {
    var service = new DeferredEmailPreferencesService();
    var update = service.EnqueueUpdate();
    var model = await LoadAsync(service);
    var mutation = model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    update.SetResult(new(service.Preferences with { NewsDigestFrequency = "weekly", CommunityDigestFrequency = "none" }));
    await mutation;
    Assert.Equal("weekly", model.NewsDigestFrequency);
    Assert.Equal("weekly", model.CommunityDigestFrequency);
  }

  [Fact]
  public async Task LateLoadDoesNotClobberANewerFieldMutation()
  {
    var service = new DeferredEmailPreferencesService();
    var model = await LoadAsync(service);
    var load = service.EnqueueFetch();
    var reloading = model.LoadAsync(TestContext.Current.CancellationToken);
    await load.Started.Task;
    await model.SetNewsDigestAsync("daily", TestContext.Current.CancellationToken);
    load.Completion.SetResult(new(service.Preferences with { NewsDigestFrequency = "weekly" }));
    await reloading;
    Assert.Equal("daily", model.NewsDigestFrequency);
  }

  [Fact]
  public async Task StaleLoadDoesNotAutoInitializeTimezoneOverAnExplicitTimezoneMutation()
  {
    var service = new DeferredEmailPreferencesService();
    var resolver = new CountingTimezoneResolver("America/New_York");
    var model = new NotificationPreferencesViewModel(service, resolver);
    var fetch = service.EnqueueFetch();
    var load = model.LoadAsync(TestContext.Current.CancellationToken);
    await fetch.Started.Task;
    var update = service.EnqueueUpdate();
    var mutation = model.SetTimezoneAsync("Europe/London", TestContext.Current.CancellationToken);

    fetch.Completion.SetResult(new(service.Preferences with { ModerationEmailTimezone = null }));
    await load;

    Assert.Equal("Europe/London", model.ModerationEmailTimezone);
    Assert.Single(service.Bodies);
    Assert.Equal(0, resolver.Calls);
    update.SetResult(new(service.Preferences with { ModerationEmailTimezone = "Europe/London" }));
    await mutation;
  }

  public static IEnumerable<object[]> FieldMutations()
  {
    yield return ["engagement", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetEngagementEmailsAsync(false, ct))];
    yield return ["news", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetNewsDigestAsync("daily", ct))];
    yield return ["moderation", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetModerationEmailsAsync(false, ct))];
    yield return ["community", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetCommunityDigestAsync("none", ct))];
    yield return ["cadence", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetCadenceAsync("selected_days", ct))];
    yield return ["days", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetDaysAsync([1, 3, 5], ct))];
    yield return ["time", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetTimeAsync(new TimeSpan(15, 0, 0), ct))];
    yield return ["timezone", (Func<NotificationPreferencesViewModel, CancellationToken, Task>)((model, ct) => model.SetTimezoneAsync("UTC", ct))];
  }

  private static async Task<NotificationPreferencesViewModel> LoadAsync(DeferredEmailPreferencesService service)
  {
    var model = new NotificationPreferencesViewModel(service, new NoTimezoneResolver());
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.Bodies.Clear();
    return model;
  }

  private static void AssertOnly(UpdateEmailPreferencesBody body, string field)
  {
    var populated = new Dictionary<string, bool>
    {
      ["engagement"] = body.EngagementEmailsEnabled.HasValue,
      ["news"] = body.NewsDigestFrequency is not null,
      ["moderation"] = body.ModerationEmailsEnabled.HasValue,
      ["community"] = body.CommunityDigestFrequency is not null,
      ["cadence"] = body.ModerationEmailCadence is not null,
      ["days"] = body.ModerationEmailDaysOfWeek is not null,
      ["time"] = body.ModerationEmailTimeOfDay is not null,
      ["timezone"] = body.ModerationEmailTimezone is not null,
    };
    Assert.Equal([field], populated.Where(pair => pair.Value).Select(pair => pair.Key));
  }

  private sealed class NoTimezoneResolver : INotificationTimezoneResolver
  {
    public string? ResolveIanaTimezone() => null;
  }

  private sealed class CountingTimezoneResolver(string timezone) : INotificationTimezoneResolver
  {
    public int Calls { get; private set; }
    public string? ResolveIanaTimezone()
    {
      Calls++;
      return timezone;
    }
  }

  private sealed class DeferredEmailPreferencesService : INotificationPreferencesService
  {
    public EmailPreferences Preferences { get; set; } = new(true, "weekly", true, "weekly", "daily", [1, 2, 3], "09:00", "UTC");
    public List<UpdateEmailPreferencesBody> Bodies { get; } = [];
    private readonly Queue<TaskCompletionSource<EmailPreferencesResponse>> updates = [];
    private readonly Queue<TaskCompletionSource<bool>> updateStarts = [];
    private TaskCompletionSource<EmailPreferencesResponse>? fetch;
    private DeferredFetch? deferredFetch;
    public TaskCompletionSource<EmailPreferencesResponse> EnqueueUpdate() { var gate = new TaskCompletionSource<EmailPreferencesResponse>(); updates.Enqueue(gate); return gate; }
    public TaskCompletionSource<bool> EnqueueUpdateStarted() { var started = new TaskCompletionSource<bool>(); updateStarts.Enqueue(started); return started; }
    public DeferredFetch EnqueueFetch() { var result = new DeferredFetch(); deferredFetch = result; fetch = result.Completion; return result; }
    public Task<EmailPreferencesResponse> FetchEmailPreferencesAsync(CancellationToken cancellationToken = default)
    {
      if (fetch is null) return Task.FromResult(new EmailPreferencesResponse(Preferences));
      var pending = fetch;
      fetch = null;
      deferredFetch?.Started.SetResult(true);
      deferredFetch = null;
      return pending.Task;
    }
    public Task<EmailPreferencesResponse> UpdateEmailPreferencesAsync(UpdateEmailPreferencesBody body, CancellationToken cancellationToken = default)
    {
      Bodies.Add(body);
      if (updateStarts.Count > 0) updateStarts.Dequeue().SetResult(true);
      return updates.Count > 0 ? updates.Dequeue().Task : Task.FromResult(new EmailPreferencesResponse(Updated(body)));
    }
    private EmailPreferences Updated(UpdateEmailPreferencesBody body) => Preferences with
    {
      EngagementEmailsEnabled = body.EngagementEmailsEnabled ?? Preferences.EngagementEmailsEnabled,
      NewsDigestFrequency = body.NewsDigestFrequency ?? Preferences.NewsDigestFrequency,
      ModerationEmailsEnabled = body.ModerationEmailsEnabled ?? Preferences.ModerationEmailsEnabled,
      CommunityDigestFrequency = body.CommunityDigestFrequency ?? Preferences.CommunityDigestFrequency,
      ModerationEmailCadence = body.ModerationEmailCadence ?? Preferences.ModerationEmailCadence,
      ModerationEmailDaysOfWeek = body.ModerationEmailDaysOfWeek ?? Preferences.ModerationEmailDaysOfWeek,
      ModerationEmailTimeOfDay = body.ModerationEmailTimeOfDay ?? Preferences.ModerationEmailTimeOfDay,
      ModerationEmailTimezone = body.ModerationEmailTimezone ?? Preferences.ModerationEmailTimezone,
    };
  }

  private sealed class DeferredFetch
  {
    public TaskCompletionSource<EmailPreferencesResponse> Completion { get; } = new();
    public TaskCompletionSource<bool> Started { get; } = new();
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}

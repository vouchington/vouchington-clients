using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Settings;

public sealed partial class NotificationPreferencesViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private const string FallbackTimezone = "America/Los_Angeles";
  private readonly INotificationPreferencesService service;
  private readonly INotificationTimezoneResolver timezoneResolver;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private EmailPreferences saved = Defaults();
  private long loadGeneration;
  private readonly Dictionary<string, SortedDictionary<long, object>> mutations = [];
  private readonly Dictionary<string, SemaphoreSlim> mutationLocks = [];
  private readonly Dictionary<string, long> fieldGenerations = [];
  private readonly Dictionary<string, long> visibleFieldGenerations = [];
  private bool engagementEmailsEnabled, moderationEmailsEnabled, isLoading, hasLoadedPreferences;
  private (string Field, long Generation)? errorOwner;
  private string newsDigestFrequency = "weekly", communityDigestFrequency = "weekly", moderationEmailCadence = "daily", moderationEmailTimeOfDay = "09:00", moderationEmailTimezone = FallbackTimezone;
  private IReadOnlyList<int> moderationEmailDaysOfWeek = [1, 2, 3, 4, 5];

  public NotificationPreferencesViewModel(
      INotificationPreferencesService service,
      INotificationTimezoneResolver? timezoneResolver = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service;
    this.timezoneResolver = timezoneResolver ?? new SystemNotificationTimezoneResolver();
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
    DigestOptions = LocalizedOptions(SettingsOptionSets.DigestFrequencyOptions);
    CadenceOptions = LocalizedOptions(SettingsOptionSets.ModerationEmailCadenceOptions);
  }

  public bool IsLoading
  {
    get => isLoading;
    private set { if (SetProperty(ref isLoading, value)) RefreshEnabledState(); }
  }
  public bool HasLoadedPreferences { get => hasLoadedPreferences; private set { if (SetProperty(ref hasLoadedPreferences, value)) RefreshEnabledState(); } }
  public bool EngagementEmailsEnabled { get => engagementEmailsEnabled; set => SetProperty(ref engagementEmailsEnabled, value); }
  public bool ModerationEmailsEnabled { get => moderationEmailsEnabled; set { if (SetProperty(ref moderationEmailsEnabled, value)) { OnPropertyChanged(nameof(ShowsModerationSchedule)); OnPropertyChanged(nameof(ShowsSelectedDays)); } } }
  public bool ShowsModerationSchedule => ModerationEmailsEnabled;
  public string NewsDigestFrequency { get => newsDigestFrequency; set => SetProperty(ref newsDigestFrequency, value); }
  public string CommunityDigestFrequency { get => communityDigestFrequency; set => SetProperty(ref communityDigestFrequency, value); }
  public string ModerationEmailCadence { get => moderationEmailCadence; set { if (SetProperty(ref moderationEmailCadence, value)) OnPropertyChanged(nameof(ShowsSelectedDays)); } }
  public bool ShowsSelectedDays => ShowsModerationSchedule && ModerationEmailCadence == "selected_days";
  public IReadOnlyList<int> ModerationEmailDaysOfWeek { get => moderationEmailDaysOfWeek; private set => SetProperty(ref moderationEmailDaysOfWeek, value); }
  public string ModerationEmailTimeOfDay { get => moderationEmailTimeOfDay; set => SetProperty(ref moderationEmailTimeOfDay, value); }
  public string ModerationEmailTimezone { get => moderationEmailTimezone; set => SetProperty(ref moderationEmailTimezone, value); }
  public bool IsPending(string field) => mutations.TryGetValue(field, out var pending) && pending.Count > 0;
  public bool IsFieldEnabled(string field) => HasLoadedPreferences && !IsLoading && !IsPending(field);
  public bool IsEngagementEnabled => IsFieldEnabled("engagement");
  public bool IsNewsEnabled => IsFieldEnabled("news");
  public bool IsModerationEnabled => IsFieldEnabled("moderation");
  public bool IsCommunityEnabled => IsFieldEnabled("community");
  public bool IsCadenceEnabled => IsFieldEnabled("cadence");
  public bool IsDaysEnabled => IsFieldEnabled("days");
  public bool IsTimeEnabled => IsFieldEnabled("time");
  public bool IsTimezoneEnabled => IsFieldEnabled("timezone");
  public IReadOnlyList<SettingsOptionViewModel> DigestOptions { get; private set; }
  public IReadOnlyList<SettingsOptionViewModel> CadenceOptions { get; private set; }
  public string? ErrorMessage => errorOwner is null ? null : localization.Localize(UiMessageKey.NativeDotnetSettingsNotificationSettingsSaveError);
  public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);
  public void Dispose() => localeSubscription?.Dispose();
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    var generation = ++loadGeneration;
    var fieldVersions = new Dictionary<string, long>(fieldGenerations);
    foreach (var field in mutations.Keys) fieldVersions[field] = -1;
    IsLoading = true;
    try { var response = await service.FetchEmailPreferencesAsync(cancellationToken).ConfigureAwait(true); if (generation != loadGeneration) return; Apply(response.EmailPreferences, fieldVersions); HasLoadedPreferences = true; await InitializeTimezoneAsync(fieldVersions.GetValueOrDefault("timezone"), cancellationToken).ConfigureAwait(true); }
    finally { if (generation == loadGeneration) IsLoading = false; }
  }
  public Task SetEngagementEmailsAsync(bool value, CancellationToken ct = default) => MutateAsync("engagement", value, p => new(EngagementEmailsEnabled: p), ct);
  public Task SetNewsDigestAsync(string value, CancellationToken ct = default) => MutateAsync("news", value, p => new(NewsDigestFrequency: p), ct);
  public Task SetModerationEmailsAsync(bool value, CancellationToken ct = default) => MutateAsync("moderation", value, p => new(ModerationEmailsEnabled: p), ct);
  public Task SetCommunityDigestAsync(string value, CancellationToken ct = default) => MutateAsync("community", value, p => new(CommunityDigestFrequency: p), ct);
  public Task SetCadenceAsync(string value, CancellationToken ct = default) => MutateAsync("cadence", value, p => new(ModerationEmailCadence: p), ct);
  public Task SetDaysAsync(IReadOnlyList<int> value, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(value);
    var days = value.ToArray();
    return days.Length == 0 ? Task.CompletedTask : MutateAsync("days", days, p => new(ModerationEmailDaysOfWeek: p), ct);
  }
  public Task SetTimeAsync(TimeSpan value, CancellationToken ct = default) => MutateAsync("time", value.ToString(@"hh\:mm", CultureInfo.InvariantCulture), p => new(ModerationEmailTimeOfDay: p), ct);
  public Task SetTimezoneAsync(string value, CancellationToken ct = default) => MutateAsync("timezone", value, p => new(ModerationEmailTimezone: p), ct);
  private async Task InitializeTimezoneAsync(long capturedTimezoneGeneration, CancellationToken ct)
  {
    if (saved.ModerationEmailTimezone is { Length: > 0 } ||
        fieldGenerations.GetValueOrDefault("timezone") != capturedTimezoneGeneration) return;
    var zone = timezoneResolver.ResolveIanaTimezone(); ModerationEmailTimezone = SystemNotificationTimezoneResolver.IsIana(zone) ? zone! : FallbackTimezone;
    if (SystemNotificationTimezoneResolver.IsIana(zone)) await SetTimezoneAsync(zone!, ct).ConfigureAwait(true);
  }
  private async Task MutateAsync<T>(string field, T value, Func<T, UpdateEmailPreferencesBody> body, CancellationToken ct)
  {
    var generation = fieldGenerations[field] = fieldGenerations.GetValueOrDefault(field) + 1;
    ClearError(field, generation);
    visibleFieldGenerations[field] = generation;
    PendingMutations(field)[generation] = value!;
    RefreshEnabledState();
    ApplyField(field, value!);
    await MutateSerializedAsync(field, generation, value, body, ct).ConfigureAwait(true);
  }
  private async Task MutateSerializedAsync<T>(
      string field, long generation, T value, Func<T, UpdateEmailPreferencesBody> body, CancellationToken ct)
  {
    var held = false;
    try
    {
      ct.ThrowIfCancellationRequested();
      await MutationLock(field).WaitAsync(ct).ConfigureAwait(true);
      held = true;
      ct.ThrowIfCancellationRequested();
      var response = await service.UpdateEmailPreferencesAsync(body(value), ct).ConfigureAwait(true);
      saved = Merge(saved, response.EmailPreferences, field);
      if (visibleFieldGenerations.GetValueOrDefault(field) == generation) ApplyField(field, Field(saved, field));
      ClearError(field, generation);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
      RestoreAfterCancellation(field, generation);
      throw;
    }
    catch
    {
      if (visibleFieldGenerations.GetValueOrDefault(field) == generation) ApplyField(field, Field(saved, field));
      if (visibleFieldGenerations.GetValueOrDefault(field) == generation) SetError(field, generation);
      throw;
    }
    finally
    {
      var pending = PendingMutations(field);
      pending.Remove(generation);
      if (pending.Count == 0) mutations.Remove(field);
      if (held) MutationLock(field).Release();
      RefreshEnabledState();
    }
  }
  private SortedDictionary<long, object> PendingMutations(string field)
  {
    if (mutations.TryGetValue(field, out var pending)) return pending;
    pending = [];
    mutations[field] = pending;
    return pending;
  }
  private void RestoreAfterCancellation(string field, long generation)
  {
    if (fieldGenerations.GetValueOrDefault(field) == generation) fieldGenerations[field] = generation + 1;
    if (visibleFieldGenerations.GetValueOrDefault(field) != generation) return;
    var previous = PendingMutations(field).LastOrDefault(pair => pair.Key < generation);
    if (previous.Key == 0) { visibleFieldGenerations.Remove(field); ApplyField(field, Field(saved, field)); }
    else { visibleFieldGenerations[field] = previous.Key; ApplyField(field, previous.Value); }
  }
  private SemaphoreSlim MutationLock(string field)
  {
    if (mutationLocks.TryGetValue(field, out var mutationLock)) return mutationLock;
    mutationLock = new SemaphoreSlim(1, 1);
    mutationLocks[field] = mutationLock;
    return mutationLock;
  }
  private void SetError(string field, long generation) { errorOwner = (field, generation); NotifyErrorChanged(); }
  private void ClearError(string field, long generation) { if (errorOwner is { } owner && owner.Field == field && owner.Generation <= generation) { errorOwner = null; NotifyErrorChanged(); } }
  private void NotifyErrorChanged() { OnPropertyChanged(nameof(ErrorMessage)); OnPropertyChanged(nameof(HasErrorMessage)); }
  private void RefreshEnabledState()
  {
    OnPropertyChanged(nameof(IsEngagementEnabled)); OnPropertyChanged(nameof(IsNewsEnabled));
    OnPropertyChanged(nameof(IsModerationEnabled)); OnPropertyChanged(nameof(IsCommunityEnabled));
    OnPropertyChanged(nameof(IsCadenceEnabled)); OnPropertyChanged(nameof(IsDaysEnabled));
    OnPropertyChanged(nameof(IsTimeEnabled)); OnPropertyChanged(nameof(IsTimezoneEnabled));
  }
  private SettingsOptionViewModel[] LocalizedOptions(
      IReadOnlyList<SettingsOptionDefinition> values) =>
      values.Select(option => new SettingsOptionViewModel(option.Value, option.LabelText, localization)).ToArray();
  private void Apply(EmailPreferences value, IReadOnlyDictionary<string, long>? fieldVersions = null)
  {
    foreach (var field in Fields)
    {
      if (fieldVersions is not null && fieldGenerations.GetValueOrDefault(field) > fieldVersions.GetValueOrDefault(field)) continue;
      saved = Merge(saved, value, field);
      ApplyField(field, Field(value, field));
    }
  }
  private void ApplyField(string f, object value) { switch (f) { case "engagement": EngagementEmailsEnabled = (bool)value; break; case "news": NewsDigestFrequency = (string)value; break; case "moderation": ModerationEmailsEnabled = (bool)value; break; case "community": CommunityDigestFrequency = (string)value; break; case "cadence": ModerationEmailCadence = (string)value; break; case "days": ModerationEmailDaysOfWeek = (IReadOnlyList<int>)value; break; case "time": ModerationEmailTimeOfDay = (string)value; break; default: ModerationEmailTimezone = (string)value; break; } }
  private static object Field(EmailPreferences p, string f) => f switch { "engagement" => p.EngagementEmailsEnabled, "news" => p.NewsDigestFrequency, "moderation" => p.ModerationEmailsEnabled, "community" => p.CommunityDigestFrequency, "cadence" => p.ModerationEmailCadence, "days" => p.ModerationEmailDaysOfWeek, "time" => p.ModerationEmailTimeOfDay, _ => p.ModerationEmailTimezone ?? FallbackTimezone };
  private static EmailPreferences Merge(EmailPreferences s, EmailPreferences r, string f) => f switch { "engagement" => s with { EngagementEmailsEnabled = r.EngagementEmailsEnabled }, "news" => s with { NewsDigestFrequency = r.NewsDigestFrequency }, "moderation" => s with { ModerationEmailsEnabled = r.ModerationEmailsEnabled }, "community" => s with { CommunityDigestFrequency = r.CommunityDigestFrequency }, "cadence" => s with { ModerationEmailCadence = r.ModerationEmailCadence }, "days" => s with { ModerationEmailDaysOfWeek = r.ModerationEmailDaysOfWeek }, "time" => s with { ModerationEmailTimeOfDay = r.ModerationEmailTimeOfDay }, _ => s with { ModerationEmailTimezone = r.ModerationEmailTimezone } };
  private static EmailPreferences Defaults() => new(true, "weekly", true, "weekly", "daily", [1, 2, 3, 4, 5], "09:00", null);
  private static readonly string[] Fields = ["engagement", "news", "moderation", "community", "cadence", "days", "time", "timezone"];
}

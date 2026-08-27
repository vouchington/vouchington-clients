namespace Voucha.Client.App.Pages;

public sealed partial class NotificationPreferencesView : ContentView, Voucha.Client.Core.Localization.IUiLocaleChangeListener
{
  private bool isSynchronizing;
  private Voucha.Client.Core.Settings.NotificationPreferencesViewModel? model;
  private Voucha.Client.Core.Localization.IUiLocalization localization = Voucha.Client.Core.Localization.UiLocalization.English;
  private IDisposable? localeSubscription;

  public NotificationPreferencesView()
  {
    InitializeComponent();
    BindingContextChanged += OnBindingContextChanged;
  }
  public event EventHandler<NotificationPreferenceChangedEventArgs>? PreferenceChanged;
  public VisualElement Heading => NotificationPreferencesHeading;
  public void ConfigureLocalization(
      Voucha.Client.Core.Localization.IUiLocalization localization,
      Voucha.Client.Core.Localization.IUiLocaleController localeController)
  {
    this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
    localeSubscription?.Dispose();
    localeSubscription = (localeController ?? throw new ArgumentNullException(nameof(localeController)))
        .SubscribeLocaleChanges(this);
    SynchronizeControls();
  }
  public void OnUiLocaleChanged() => SynchronizeControls();
  public void SynchronizeControls()
  {
    if (BindingContext is not Voucha.Client.Core.Settings.NotificationPreferencesViewModel model) return;
    isSynchronizing = true;
    try
    {
      EngagementSwitch.IsToggled = model.EngagementEmailsEnabled;
      ModerationSwitch.IsToggled = model.ModerationEmailsEnabled;
      NewsPicker.SelectedItem = Option(model.DigestOptions, model.NewsDigestFrequency);
      CommunityPicker.SelectedItem = Option(model.DigestOptions, model.CommunityDigestFrequency);
      CadencePicker.SelectedItem = Option(model.CadenceOptions, model.ModerationEmailCadence);
      TimeControl.Time = TimeSpan.Parse(model.ModerationEmailTimeOfDay);
      TimezonePicker.ItemsSource = TimezoneOptions(model.ModerationEmailTimezone);
      TimezonePicker.SelectedItem = model.ModerationEmailTimezone;
      SynchronizeDays(model);
    }
    finally { isSynchronizing = false; }
  }
  private void OnEngagementChanged(object? s, ToggledEventArgs e) => Raise("engagement", e.Value);
  private void OnModerationChanged(object? s, ToggledEventArgs e) => Raise("moderation", e.Value);
  private void OnNewsChanged(object? s, EventArgs e) => Raise("news", Value(NewsPicker));
  private void OnCommunityChanged(object? s, EventArgs e) => Raise("community", Value(CommunityPicker));
  private void OnCadenceChanged(object? s, EventArgs e) => Raise("cadence", Value(CadencePicker));
  private void OnTimeChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e) { if (e.PropertyName == nameof(TimePicker.Time)) Raise("time", TimeControl.Time); }
  private void OnTimezoneChanged(object? s, EventArgs e) => Raise("timezone", TimezonePicker.SelectedItem as string);
  private void OnDayClicked(object? s, EventArgs e) { if (s is Button { CommandParameter: string day } && int.TryParse(day, out var value)) Raise("day", value); }
  private void Raise(string field, object? value)
  {
    if (!isSynchronizing && value is not null)
      PreferenceChanged?.Invoke(this, new(field, value));
  }
  private static string? Value(Picker picker) => (picker.SelectedItem as Voucha.Client.Core.Settings.SettingsOptionViewModel)?.Value;
  private static Voucha.Client.Core.Settings.SettingsOptionViewModel Option(
      IReadOnlyList<Voucha.Client.Core.Settings.SettingsOptionViewModel> values,
      string value) => values.FirstOrDefault(option => option.Value == value) ?? values[0];
  private void SynchronizeDays(Voucha.Client.Core.Settings.NotificationPreferencesViewModel model)
  {
    var selected = model.ModerationEmailDaysOfWeek;
    foreach (var button in DaysControls.Children.OfType<Button>())
    {
      var day = int.Parse((string)button.CommandParameter);
      var isSelected = selected.Contains(day);
      var dayName = localization.Culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(day % 7));
      button.Text = dayName;
      button.SetValue(
          SemanticProperties.DescriptionProperty,
          localization.Format(
              isSelected
                  ? Voucha.Client.Core.Localization.UiMessageKey.NativeDotnetSettingsModerationEmailDaySelected
                  : Voucha.Client.Core.Localization.UiMessageKey.NativeDotnetSettingsModerationEmailDayUnselected,
              [("day", (object?)dayName)]));
      button.FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
      button.IsEnabled = model.IsDaysEnabled && !(isSelected && selected.Count == 1);
    }
  }
  private static List<string> TimezoneOptions(string current) =>
      Voucha.Client.Core.Settings.SettingsOptionSets.ModerationEmailTimezoneOptions
          .Prepend(current)
          .Where(value => !string.IsNullOrEmpty(value))
          .Distinct(StringComparer.Ordinal)
          .ToList();
  private void OnBindingContextChanged(object? sender, EventArgs e)
  {
    if (model is not null) model.PropertyChanged -= OnModelPropertyChanged;
    model = BindingContext as Voucha.Client.Core.Settings.NotificationPreferencesViewModel;
    if (model is not null) model.PropertyChanged += OnModelPropertyChanged;
  }
  private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.EngagementEmailsEnabled) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.ModerationEmailsEnabled) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.DigestOptions) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.CadenceOptions) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.NewsDigestFrequency) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.CommunityDigestFrequency) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.ModerationEmailCadence) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.ModerationEmailTimeOfDay) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.ModerationEmailTimezone) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.IsDaysEnabled) or
        nameof(Voucha.Client.Core.Settings.NotificationPreferencesViewModel.ModerationEmailDaysOfWeek))
    {
      if (model is not null) SynchronizeControls();
    }
  }
}

public sealed class NotificationPreferenceChangedEventArgs : EventArgs
{
  public NotificationPreferenceChangedEventArgs(string field, object? value)
  {
    Field = field;
    Value = value;
  }

  public string Field { get; }

  public object? Value { get; }
}

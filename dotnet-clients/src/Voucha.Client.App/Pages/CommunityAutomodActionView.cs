using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class CommunityAutomodActionView : VerticalStackLayout
{
  private static readonly BindableProperty LocaleVersionProperty = BindableProperty.Create(
      nameof(LocaleVersion), typeof(int), typeof(CommunityAutomodActionView), 0,
      propertyChanged: static (view, _, _) => ((CommunityAutomodActionView)view).RefreshLocalization());
  private static readonly string[] Actions = ["record_only", "review_queue", "unpublish"];
  private static readonly UiMessageKey[] OptionKeys =
  [
    UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormRecordOnlyC9184072,
    UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormSendToReviewQueue1c3a1b74,
    UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormUnpublish2db04a54,
  ];
  private static readonly UiMessageKey[] DescriptionKeys =
  [
    UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedTheFlagAppears01b0cba0,
    UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormKeepThePostPublishedAndAddIt56f75b30,
    UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormRemoveThePostFromTheCommunityRight215c4ec5,
  ];
  private readonly Picker actionPicker = new() { AutomationId = "community-automod-action-picker" };
  private readonly Label description = new() { AutomationId = "community-automod-action-description" };
  private readonly Label status = new() { AutomationId = "community-automod-action-status" };
  private readonly Button saveButton = UiCopy.Bind(
      new Button { AutomationId = "community-automod-action-save" },
      Button.TextProperty,
      UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormSaveAutomodAction5f2b209e);
  private string? committedAction;
  private UiMessageKey? statusKey;
  private int LocaleVersion { get => (int)GetValue(LocaleVersionProperty); set => SetValue(LocaleVersionProperty, value); }

  public CommunityAutomodActionView(string? action, Func<string, Task<bool>>? save = null)
  {
    SaveRequested = save ?? (_ => Task.FromResult(false));
    Spacing = 3;
    Children.Add(UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormAutomodAction83211784));
    Children.Add(UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormChooseWhatHappensToApublishedPostB2e29694));
    Children.Add(actionPicker);
    Children.Add(description);
    Children.Add(saveButton);
    Children.Add(status);
    actionPicker.SelectedIndexChanged += (_, _) => RefreshDescription();
    saveButton.Clicked += OnSaveClicked;
    Update(action);
    if (Application.Current?.Resources.TryGetValue("UiLocaleVersion", out var resource) == true &&
        resource is UiLocaleVersion version)
      SetBinding(LocaleVersionProperty, new Binding(nameof(UiLocaleVersion.Version), source: version));
  }

  public Func<string, Task<bool>> SaveRequested { get; set; }
  public string? SelectedAction => actionPicker.SelectedIndex is >= 0 and < 3
      ? Actions[actionPicker.SelectedIndex]
      : null;

  public void Update(string? action)
  {
    var selectedIndex = actionPicker.SelectedIndex;
    actionPicker.ItemsSource = OptionKeys.Select(UiCopy.Localize).ToArray();
    if (!string.Equals(committedAction, action, StringComparison.Ordinal))
    {
      committedAction = action;
      selectedIndex = Array.IndexOf(Actions, action);
    }
    actionPicker.SelectedIndex = selectedIndex;
    RefreshDescription();
    IsVisible = !string.IsNullOrWhiteSpace(action);
  }

  private void RefreshDescription() =>
      description.Text = actionPicker.SelectedIndex is >= 0 and < 3
          ? UiCopy.Localize(DescriptionKeys[actionPicker.SelectedIndex])
          : null;

  private void RefreshLocalization()
  {
    Update(committedAction);
    if (statusKey is { } key) status.Text = UiCopy.Localize(key);
  }

  private void SetStatus(UiMessageKey key)
  {
    statusKey = key;
    status.Text = UiCopy.Localize(key);
  }

  private async void OnSaveClicked(object? sender, EventArgs args)
  {
    if (!saveButton.IsEnabled || SelectedAction is not { } action) return;
    saveButton.IsEnabled = false;
    SetStatus(UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormSavingDc85af8f);
    try
    {
      if (await SaveRequested(action).ConfigureAwait(true))
        SetStatus(UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormAutomodActionSaved5a7127b1);
      else
      {
        actionPicker.SelectedIndex = Array.IndexOf(Actions, committedAction);
        SetStatus(UiMessageKey.ExtractedCommunitiesCommunityAutomodActionFormCouldNotSaveTheAutomodActionD095dea5);
      }
    }
    finally
    {
      saveButton.IsEnabled = true;
    }
  }
}

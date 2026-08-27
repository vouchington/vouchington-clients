using System.ComponentModel;
using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class DynamicConfigPage : ContentPage
{
  private readonly DynamicConfigViewModel viewModel;

  public DynamicConfigPage(DynamicConfigViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnViewModelChanged;
    RenderContent();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadAsync().ConfigureAwait(true);
    RenderContent();
  }

  private async void OnNamespaceSelected(object? sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is DynamicConfigNamespaceOption item)
    {
      await viewModel.SelectAsync(item.ProtocolNamespace).ConfigureAwait(true);
    }
  }

  private async void OnRefreshClicked(object? sender, EventArgs e)
  {
    if (viewModel.SelectedNamespace is { } selected) await viewModel.SelectAsync(selected.Namespace).ConfigureAwait(true);
    else await viewModel.LoadAsync().ConfigureAwait(true);
  }

  private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(DynamicConfigViewModel.Fields) or nameof(DynamicConfigViewModel.History)) RenderContent();
  }

  private void RenderContent()
  {
    FieldsLayout.Children.Clear();
    foreach (var field in viewModel.Fields) FieldsLayout.Children.Add(CreateFieldCard(field));
    HistoryLayout.Children.Clear();
    foreach (var entry in viewModel.History) HistoryLayout.Children.Add(CreateHistoryCard(entry));
  }

  private View CreateFieldCard(DynamicConfigFieldViewModel field)
  {
    var content = new VerticalStackLayout { Spacing = 6 };
    content.Add(new Label { Text = field.ProtocolName, FontAttributes = FontAttributes.Bold });
    content.Add(new Label { Text = field.ExternalContentDescription, FontSize = 12 });
    if (ConstraintText(field) is { } constraint) content.Add(new Label { Text = constraint, FontSize = 12, AutomationId = $"dynamic-config-{field.Name}-constraint" });
    if (field.IsBoolean) content.Add(CreateBooleanEditor(field));
    else content.Add(CreateDraftEditor(field));
    var validation = new Label { TextColor = Color.FromArgb("#991B1B"), AutomationId = $"dynamic-config-{field.Name}-validation", BindingContext = field };
    validation.SetBinding(Label.TextProperty, nameof(DynamicConfigFieldViewModel.ValidationMessage));
    validation.SetBinding(IsVisibleProperty, nameof(DynamicConfigFieldViewModel.HasValidationError));
    content.Add(validation);
    return new Border { AutomationId = $"dynamic-config-{field.Name}-field", Padding = 12, Stroke = Color.FromArgb("#D8DEE9"), Content = content };
  }

  private View CreateBooleanEditor(DynamicConfigFieldViewModel field)
  {
    var toggle = new Switch { AutomationId = $"dynamic-config-{field.Name}-global", IsToggled = field.BooleanValue, BindingContext = viewModel };
    toggle.SetBinding(IsEnabledProperty, nameof(DynamicConfigViewModel.CanMutate));
    var isReconciling = false;
    toggle.Toggled += async (_, args) =>
    {
      if (isReconciling) return;
      await viewModel.SaveBooleanAsync(field, args.Value).ConfigureAwait(true);
      if (toggle.IsToggled == field.BooleanValue) return;
      isReconciling = true;
      toggle.IsToggled = field.BooleanValue;
      isReconciling = false;
    };
    return toggle;
  }

  private View CreateDraftEditor(DynamicConfigFieldViewModel field)
  {
    var entry = new Entry { AutomationId = $"dynamic-config-{field.Name}-value", Text = field.Draft, BindingContext = viewModel, Keyboard = field.IsNumber ? Keyboard.Numeric : Keyboard.Default };
    entry.SetBinding(IsEnabledProperty, nameof(DynamicConfigViewModel.CanMutate));
    entry.TextChanged += (_, args) => field.Draft = args.NewTextValue ?? string.Empty;
    var save = UiCopy.Bind(
        new Button { AutomationId = $"dynamic-config-{field.Name}-save", BindingContext = viewModel },
        Button.TextProperty,
        UiMessageKey.CommonSave);
    save.SetBinding(IsEnabledProperty, nameof(DynamicConfigViewModel.CanMutate));
    save.Clicked += async (_, _) =>
    {
      await viewModel.SaveDraftAsync(field).ConfigureAwait(true);
      if (field.HasValidationError)
        await DisplayAlertAsync(
            UiCopy.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsInvalidValue),
            field.ValidationMessage!,
            UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk)).ConfigureAwait(true);
    };
    return new HorizontalStackLayout { Spacing = 8, Children = { entry, save } };
  }

  private static View CreateHistoryCard(DynamicConfigHistoryEntry entry)
  {
    var actor = entry.ChangedBy?.Username ?? entry.ChangedBy?.Id;
    var actorText = actor is null
        ? UiText.Localized(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDeletedUser)
        : UiText.UserContent(actor);
    var fields = string.Join(", ", (entry.ChangedFields ?? new Dictionary<string, DynamicConfigFieldChange>())
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => $"{pair.Key}: {DisplayValue(pair.Value.Previous)} → {DisplayValue(pair.Value.Next)}"));
    return new Label
    {
      Text = UiCopy.Format(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsHistoryEntry,
          ("date", UiCopy.FormatDateTime(entry.CreatedAt)),
          ("actor", actorText),
          ("fields", UiText.ProtocolValue(fields))),
    };
  }

  private static string DisplayValue(DynamicConfigValue value) => value switch
  {
    DynamicConfigBooleanValue boolean => boolean.Value ? "true" : "false",
    DynamicConfigNumericValue number => number.Value.ToString(CultureInfo.InvariantCulture),
    DynamicConfigStringValue text => text.Value,
    _ => "unknown",
  };

  private static string? ConstraintText(DynamicConfigFieldViewModel field)
  {
    if (!field.IsNumber) return null;
    var kind = UiCopy.Localize(field.Field.IsInteger
        ? UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsWholeNumber
        : UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumber);
    return (field.Field.MinValue, field.Field.MaxValue) switch
    {
      ({ } minimum, { } maximum) => UiCopy.Format(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumberRange,
          ("kind", kind),
          ("minimum", minimum),
          ("maximum", maximum)),
      ({ } minimum, null) => UiCopy.Format(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumberMinimum,
          ("kind", kind),
          ("minimum", minimum)),
      (null, { } maximum) => UiCopy.Format(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumberMaximum,
          ("kind", kind),
          ("maximum", maximum)),
      _ => UiCopy.Format(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsNumberKind,
          ("kind", kind)),
    };
  }
}

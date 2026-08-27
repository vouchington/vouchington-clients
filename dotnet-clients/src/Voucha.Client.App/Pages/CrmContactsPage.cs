using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using Voucha.Client.Core.Crm;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class CrmContactsPage : ContentPage
{
  private readonly CrmContactsViewModel viewModel;
  private readonly IServiceProvider serviceProvider;
  private readonly Entry searchEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmSearch);
  private readonly Picker statusPicker = new() { SelectedIndex = 0 };
  private readonly Picker verticalPicker = new() { SelectedIndex = 0 };
  private readonly Picker linkedPicker = new() { ItemsSource = new[] { UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCrmAny), UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCrmLinked), UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCrmUnlinked) }, SelectedIndex = 0 };
  private readonly Entry nameEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmName);
  private readonly Entry emailEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmEmail);
  private readonly Entry phoneEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmPhone);
  private readonly Picker createVerticalPicker = new() { SelectedIndex = 0 };
  private readonly Picker createTypePicker = new() { SelectedIndex = 0 };
  private readonly Entry createFollowerCountEntry = UiCopy.Bind(new Entry { Keyboard = Keyboard.Numeric }, Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmFollowerCount);
  private readonly Editor createNotesEditor = UiCopy.Bind(new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 88 }, Editor.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmCreateNotes);
  private readonly Editor importCsvEditor = UiCopy.Bind(new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 120 }, Editor.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmCsvImport);
  private readonly Label errorLabel = new() { TextColor = Colors.IndianRed };
  private readonly CollectionView contactsView = new() { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };

  public CrmContactsPage(CrmContactsViewModel viewModel, IServiceProvider serviceProvider)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    ConfigureTaxonomyPicker(statusPicker, viewModel.StatusFilterOptions);
    ConfigureTaxonomyPicker(verticalPicker, viewModel.VerticalFilterOptions);
    ConfigureTaxonomyPicker(createVerticalPicker, viewModel.CreateVerticalOptions);
    ConfigureTaxonomyPicker(createTypePicker, viewModel.CreateContactTypeOptions);
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpCrmCrm.Value);
    contactsView.ItemTemplate = new DataTemplate(BuildContactTemplate);
    ConfigurePagination();
    Content = new ScrollView { Content = BuildLayout() };
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page load failures are displayed in page state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
      contactsView.ItemsSource = viewModel.Contacts;
    }
    catch (Exception ex)
    {
      errorLabel.Text = ex.Message;
    }
  }

  private VerticalStackLayout BuildLayout() =>
      new()
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          UiCopy.Bind(new Label { FontSize = 24, FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmCrm),
          BuildFilterRow(),
          BuildCreateSection(),
          BuildImportSection(),
          errorLabel,
          contactsView,
        },
      };

  private View BuildFilterRow() =>
      new HorizontalStackLayout
      {
        Spacing = 8,
        Children =
        {
          searchEntry,
          statusPicker,
          verticalPicker,
          new VerticalStackLayout
          {
            Spacing = 4,
            Children = { UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmLinked), linkedPicker },
          },
          BuildButton(UiMessageKey.NativeDotnetCsharpCrmSearch, OnSearchClicked),
        },
      };

  private View BuildCreateSection() =>
      new VerticalStackLayout
      {
        Spacing = 8,
        Children =
        {
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmCreateContact),
          nameEntry,
          emailEntry,
          phoneEntry,
          createVerticalPicker,
          createTypePicker,
          createFollowerCountEntry,
          createNotesEditor,
          BuildButton(UiMessageKey.NativeDotnetCsharpCrmCreate, OnCreateClicked),
        },
      };

  private View BuildImportSection() =>
      new VerticalStackLayout
      {
        Spacing = 8,
        Children =
        {
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmImportCsv),
          importCsvEditor,
          BuildButton(UiMessageKey.NativeDotnetCsharpCrmImport, OnImportClicked),
        },
      };

  private View BuildContactTemplate()
  {
    var name = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
    name.SetBinding(Label.TextProperty, nameof(CrmContactRow.Name));
    var email = new Label();
    email.SetBinding(Label.TextProperty, nameof(CrmContactRow.Email));
    var meta = new Label();
    meta.SetBinding(Label.TextProperty, nameof(CrmContactRow.LocalizedVertical));
    var status = new Label();
    status.SetBinding(Label.TextProperty, nameof(CrmContactRow.LocalizedStatus));
    var open = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpOpen);
    open.Clicked += OnOpenClicked;
    open.SetBinding(Button.CommandParameterProperty, new Binding("."));
    return new Border
    {
      Stroke = Colors.LightGray,
      StrokeThickness = 1,
      Padding = 12,
      Content = new VerticalStackLayout { Spacing = 4, Children = { name, email, meta, status, open } },
    };
  }

  private static Button BuildButton(UiMessageKey key, EventHandler handler)
  {
    var button = UiCopy.Bind(new Button(), Button.TextProperty, key);
    button.Clicked += handler;
    return button;
  }

  private static string? PickerValue(Picker picker) =>
      picker.SelectedIndex > 0 && picker.SelectedItem is UiProtocolOption option
          ? option.ProtocolValue
          : null;

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
  {
    if (args.PropertyName is null or nameof(CrmContactsViewModel.Contacts))
    {
      RefreshPickerLabels();
      contactsView.ItemsSource = viewModel.Contacts;
    }
  }

  private void RefreshPickerLabels()
  {
    ResetPickerItems(statusPicker, viewModel.StatusFilterOptions);
    ResetPickerItems(verticalPicker, viewModel.VerticalFilterOptions);
    ResetPickerItems(linkedPicker, UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCrmAny), UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCrmLinked), UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCrmUnlinked));
    ResetPickerItems(createVerticalPicker, viewModel.CreateVerticalOptions);
    ResetPickerItems(createTypePicker, viewModel.CreateContactTypeOptions);
  }

  private static void ConfigureTaxonomyPicker(
      Picker picker,
      IReadOnlyList<UiProtocolOption> options)
  {
    picker.ItemDisplayBinding = new Binding(nameof(UiProtocolOption.DisplayLabel));
    picker.ItemsSource = options;
  }

  private static void ResetPickerItems(
      Picker picker,
      IReadOnlyList<UiProtocolOption> options)
  {
    var selectedIndex = picker.SelectedIndex;
    picker.ItemsSource = options;
    picker.SelectedIndex = Math.Clamp(selectedIndex, 0, options.Count - 1);
  }
}

using System.ComponentModel;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed partial class ModerationReportsPage : ContentPage
{
  private sealed record ModeOption(string Label, ModerationReportsMode Mode);

  private readonly ModerationReportsViewModel viewModel;
  private readonly VerticalStackLayout queue = new() { Spacing = 12 };
  private readonly Label errorLabel = new() { TextColor = Colors.IndianRed };
  private readonly Label noticeLabel = new() { TextColor = Colors.DarkGoldenrod };
  private readonly Picker statusPicker = new();
  private readonly Picker modePicker = new();
  private readonly Picker sortPicker = new();
  private readonly HybridPaginationControl paginationControl = new() { PaginationId = "moderation-reports" };
  private CancellationTokenSource? loadCancellation;
  private bool applyingPickerState;

  public ModerationReportsPage(ModerationReportsViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    Title = UiCopy.Localize(UiMessageKey.NativeDotnetModerationReports);
    BindingContext = viewModel;
    ConfigurePickers();
    viewModel.PropertyChanged += ViewModelPropertyChanged;
    paginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
    paginationControl.SetBinding(HybridPaginationControl.HasMoreProperty, nameof(ModerationReportsViewModel.HasNextPage));
    paginationControl.SetBinding(HybridPaginationControl.IsLoadingProperty, nameof(ModerationReportsViewModel.IsLoading));
    paginationControl.SetBinding(HybridPaginationControl.HasErrorProperty, nameof(ModerationReportsViewModel.HasError));
    var scrollView = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          UiCopy.Bind(
              new Label { FontSize = 24, FontAttributes = FontAttributes.Bold },
              Label.TextProperty,
              UiMessageKey.NativeDotnetModerationReports),
          UiCopy.Bind(
              new Label(),
              Label.TextProperty,
              viewModel.IsMemberReadOnly
                  ? UiMessageKey.NativeDotnetModerationRebasedMemberQueueDescription
                  : UiMessageKey.NativeDotnetModerationRebasedStaffQueueDescription),
          FilterRow(),
          BulkActions(),
          noticeLabel,
          errorLabel,
          queue,
          paginationControl,
        },
      },
    };
    scrollView.Scrolled += (_, args) =>
    {
      if (args.ScrollY >= Math.Max(0, scrollView.ContentSize.Height - scrollView.Height - 200))
        paginationControl.TryLoadAutomatically();
    };
    Content = scrollView;
    RenderQueue();
  }

  public async Task ReloadAsync()
  {
    var token = StartLoad();
    try
    {
      await viewModel.LoadAsync(token).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (viewModel.State == LoadState.Idle) await ReloadAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    loadCancellation?.Cancel();
    base.OnDisappearing();
  }

  private View FilterRow()
  {
    var row = new HorizontalStackLayout { Spacing = 8, Children = { statusPicker } };
    if (viewModel.IsStaff) row.Children.Add(modePicker);
    if (viewModel.IsStaff)
    {
      sortPicker.IsVisible = viewModel.Mode == ModerationReportsMode.Flat;
      row.Children.Add(sortPicker);
    }
    return row;
  }

  private void ConfigurePickers()
  {
    statusPicker.Title = UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsStatus);
    modePicker.Title = UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsView);
    sortPicker.Title = UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsSort);
    statusPicker.ItemsSource = Enum.GetValues<ModerationReportStatus>().Select(StatusLabel).ToArray();
    modePicker.ItemsSource = ModeOptions();
    modePicker.ItemDisplayBinding = new Binding(nameof(ModeOption.Label));
    sortPicker.ItemsSource = new[]
    {
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsSeverity),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsMostReports),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsOldest),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsNewest),
    };
    statusPicker.SelectedIndex = (int)viewModel.Status;
    modePicker.SelectedIndex = ModeIndex(viewModel.Mode);
    sortPicker.SelectedIndex = SortIndex(viewModel.Sort);
    statusPicker.SelectedIndexChanged += async (_, _) => await ApplyFiltersAsync().ConfigureAwait(true);
    modePicker.SelectedIndexChanged += async (_, _) => await ApplyFiltersAsync().ConfigureAwait(true);
    sortPicker.SelectedIndexChanged += async (_, _) => await ApplyFiltersAsync().ConfigureAwait(true);
  }

  private async Task ApplyFiltersAsync()
  {
    if (applyingPickerState || statusPicker.SelectedIndex < 0) return;
    var mode = SelectedMode();
    var sort = sortPicker.SelectedIndex switch
    {
      0 => ModerationReportSort.Severity,
      1 => ModerationReportSort.MostReported,
      2 => ModerationReportSort.CreatedAtAsc,
      _ => ModerationReportSort.CreatedAtDesc,
    };
    var token = StartLoad();
    try
    {
      await viewModel.SetFiltersAsync(
          (ModerationReportStatus)statusPicker.SelectedIndex, mode, sort, token).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
  }

  private async Task LoadMoreAsync()
  {
    try { await viewModel.LoadMoreAsync(StartLoad()).ConfigureAwait(true); }
    catch (OperationCanceledException) { }
  }

  private async void OnLoadNextPageRequested(object? sender, EventArgs args) =>
      await LoadMoreAsync().ConfigureAwait(true);

  private CancellationToken StartLoad()
  {
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    loadCancellation = new CancellationTokenSource();
    return loadCancellation.Token;
  }

  private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
  {
    if (args.PropertyName is null or nameof(ModerationReportsViewModel.StaffReports))
      ConfigurePickerLabels();
    RenderQueue();
  }

}

using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed class ModerationAppealsPage : ContentPage
{
  private readonly ModerationAppealsViewModel viewModel;
  private readonly Picker statusPicker = UiCopy.Bind(
      new Picker(),
      Picker.TitleProperty,
      UiMessageKey.NativeSwiftModerationAppealsStatus);
  private readonly CollectionView appealsView = new() { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };
  private readonly Label statusLabel = new() { TextColor = Colors.IndianRed };
  private readonly ActivityIndicator loadingIndicator = new();
  private readonly HybridPaginationControl paginationControl = new() { PaginationId = "moderation-appeals" };
  private readonly Label emptyLabel = UiCopy.Bind(new Label
  {
    HorizontalTextAlignment = TextAlignment.Center,
    VerticalTextAlignment = TextAlignment.Center,
  }, Label.TextProperty, UiMessageKey.NativeSwiftModerationAppealsNoAppealsMatch);
  private CancellationTokenSource? loadCancellation;

  public ModerationAppealsPage(ModerationAppealsViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetModerationAppeals.Value);
    ConfigureStatusPicker();
    statusPicker.SelectedIndexChanged += StatusChangedAsync;
    appealsView.ItemTemplate = new DataTemplate(() => new ModerationAppealCardView(viewModel));
    appealsView.SetBinding(ItemsView.ItemsSourceProperty, nameof(ModerationAppealsViewModel.Appeals));
    paginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
    paginationControl.SetBinding(HybridPaginationControl.HasMoreProperty, nameof(ModerationAppealsViewModel.HasMore));
    paginationControl.SetBinding(HybridPaginationControl.IsLoadingProperty, nameof(ModerationAppealsViewModel.IsLoading));
    paginationControl.SetBinding(HybridPaginationControl.HasErrorProperty, nameof(ModerationAppealsViewModel.HasError));
    appealsView.Footer = paginationControl;
    appealsView.RemainingItemsThreshold = 2;
    appealsView.RemainingItemsThresholdReached += LoadMoreAsync;
    statusLabel.SetBinding(Label.TextProperty, nameof(ModerationAppealsViewModel.ErrorMessage));
    loadingIndicator.SetBinding(ActivityIndicator.IsRunningProperty, nameof(ModerationAppealsViewModel.IsLoading));
    loadingIndicator.SetBinding(IsVisibleProperty, nameof(ModerationAppealsViewModel.IsLoading));
    emptyLabel.SetBinding(IsVisibleProperty, nameof(ModerationAppealsViewModel.IsEmpty));
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(ModerationAppealsViewModel.Appeals))
        ConfigureStatusPicker();
    };
    Content = CreateContent();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await ReloadAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    loadCancellation = null;
    base.OnDisappearing();
  }

  private View CreateContent()
  {
    if (!viewModel.IsSignedIn)
    {
      return UiCopy.Bind(
          new Label { Margin = 16 },
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationAppealsSignInMessage);
    }
    if (!viewModel.CanAccess)
    {
      return UiCopy.Bind(
          new Label { Margin = 16 },
          Label.TextProperty,
          UiMessageKey.NativeSwiftModerationAppealsStaffAccessMessage);
    }

    var refresh = UiCopy.Bind(
        new Button(),
        Button.TextProperty,
        UiMessageKey.NativeDotnetCommonRefresh);
    refresh.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    var grid = new Grid
    {
      Padding = 16,
      RowDefinitions =
      {
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Star),
      },
    };
    grid.Add(statusPicker);
    grid.Add(refresh, 0, 1);
    grid.Add(statusLabel, 0, 2);
    grid.Add(appealsView, 0, 3);
    grid.Add(emptyLabel, 0, 3);
    grid.Add(loadingIndicator, 0, 3);
    return grid;
  }

  private async void StatusChangedAsync(object? sender, EventArgs args)
  {
    var status = statusPicker.SelectedIndex switch
    {
      0 => ModerationAppealStatus.Pending,
      1 => ModerationAppealStatus.Resolved,
      2 => ModerationAppealStatus.Dismissed,
      _ => (ModerationAppealStatus?)null,
    };
    if (status is null) return;
    await viewModel.SelectStatusAsync(status.Value, StartLoad()).ConfigureAwait(true);
  }

  private void LoadMoreAsync(object? sender, EventArgs args) =>
      paginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs args) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);

  public async Task ReloadAsync() =>
      await viewModel.LoadAsync(StartLoad()).ConfigureAwait(true);

  private CancellationToken StartLoad()
  {
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    loadCancellation = new CancellationTokenSource();
    return loadCancellation.Token;
  }

  private void ConfigureStatusPicker()
  {
    statusPicker.ItemsSource = new[]
    {
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsPending),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsResolved),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsDismissed),
    };
    statusPicker.SelectedIndex = viewModel.SelectedStatus switch
    {
      ModerationAppealStatus.Pending => 0,
      ModerationAppealStatus.Resolved => 1,
      ModerationAppealStatus.Dismissed => 2,
      _ => -1,
    };
  }
}

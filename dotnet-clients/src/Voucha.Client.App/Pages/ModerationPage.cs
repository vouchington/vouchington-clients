using Voucha.Client.App.Controls;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed class ModerationPage : ContentPage
{
  private readonly ModerationViewModel viewModel;
  private readonly CollectionView rowsView = new() { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };
  private readonly Label statusLabel = new() { TextColor = Colors.IndianRed };
  private CancellationTokenSource? loadCancellation;

  public ModerationPage(ModerationViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    SetBinding(TitleProperty, new Binding(nameof(ModerationViewModel.Title)));
    rowsView.ItemTemplate = new DataTemplate(RowTemplate);
    rowsView.SetBinding(ItemsView.ItemsSourceProperty, nameof(ModerationViewModel.Items));
    statusLabel.SetBinding(Label.TextProperty, nameof(ModerationViewModel.ErrorMessage));
    var titleLabel = new Label { FontAttributes = FontAttributes.Bold, FontSize = 20 };
    titleLabel.SetBinding(Label.TextProperty, nameof(ModerationViewModel.Title));
    var refreshButton = UiCopy.Bind(
        new Button(),
        Button.TextProperty,
        Voucha.Client.Core.Localization.UiMessageKey.NativeDotnetCsharpRefresh);
    refreshButton.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    var rangeSelector = new HorizontalStackLayout { Spacing = 6 };
    rangeSelector.SetBinding(IsVisibleProperty, nameof(ModerationViewModel.IsTransparencyContext));
    AddRangeButton(rangeSelector, UiMessageKey.NativeSwiftCommunityRowsTransparencyLatestReleasedDay, ModerationTransparencyRange.Today);
    AddRangeButton(rangeSelector, UiMessageKey.NativeSwiftGrowthDashboardMessage7d, ModerationTransparencyRange.SevenDays);
    AddRangeButton(rangeSelector, UiMessageKey.NativeSwiftGrowthDashboardMessage30d, ModerationTransparencyRange.Default);
    AddRangeButton(rangeSelector, UiMessageKey.NativeSwiftGrowthDashboardMessage90d, ModerationTransparencyRange.NinetyDays);
    AddRangeButton(rangeSelector, UiMessageKey.NativeDotnetGrowthAll, ModerationTransparencyRange.All);
    var pagination = new HybridPaginationControl { PaginationId = "moderation-cases" };
    pagination.SetBinding(HybridPaginationControl.HasMoreProperty, nameof(ModerationViewModel.HasMore));
    pagination.SetBinding(HybridPaginationControl.IsLoadingProperty, nameof(ModerationViewModel.IsLoadingMore));
    pagination.SetBinding(HybridPaginationControl.HasErrorProperty, nameof(ModerationViewModel.HasPaginationError));
    pagination.LoadNextPageRequested += OnLoadNextPageRequested;
    rowsView.Footer = pagination;
    rowsView.RemainingItemsThreshold = 2;
    rowsView.RemainingItemsThresholdReached += (_, _) => pagination.TryLoadAutomatically();
    var paginationErrorLabel = new Label { TextColor = Colors.IndianRed };
    paginationErrorLabel.SetBinding(Label.TextProperty, nameof(ModerationViewModel.PaginationErrorMessage));
    Content = new Grid
    {
      Padding = 16,
      RowDefinitions =
      {
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Auto),
        new RowDefinition(GridLength.Star),
        new RowDefinition(GridLength.Auto),
      },
    };
    ((Grid)Content).Add(titleLabel, 0, 0);
    ((Grid)Content).Add(refreshButton, 0, 1);
    ((Grid)Content).Add(rangeSelector, 0, 2);
    ((Grid)Content).Add(statusLabel, 0, 3);
    ((Grid)Content).Add(rowsView, 0, 4);
    ((Grid)Content).Add(paginationErrorLabel, 0, 5);
  }

  public void SetContext(ModerationRouteContext context) => viewModel.SetContext(context);

  public async Task ApplyContextAsync(ModerationRouteContext context)
  {
    viewModel.SetContext(context);
    await ReloadAsync().ConfigureAwait(true);
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await ReloadAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    loadCancellation?.Cancel();
    base.OnDisappearing();
  }

  private async Task ReloadAsync()
  {
    var cancellationToken = StartLoad();
    try
    {
      await viewModel.LoadAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
  }

  private CancellationToken StartLoad()
  {
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    loadCancellation = new CancellationTokenSource();
    return loadCancellation.Token;
  }

  private async void OnLoadNextPageRequested(object? sender, EventArgs args) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);

  private void AddRangeButton(HorizontalStackLayout selector, UiMessageKey label, string range)
  {
    var button = UiCopy.Bind(new Button(), Button.TextProperty, label);
    button.AutomationId = $"moderation-transparency-range-{range}";
    button.Clicked += async (_, _) => await SelectRangeAsync(range).ConfigureAwait(true);
    selector.Children.Add(button);
  }

  private async Task SelectRangeAsync(string range)
  {
    if (!ModerationTransparencyRangeSelection.ShouldStartLoad(viewModel.TransparencyRange, range)) return;
    var cancellationToken = StartLoad();
    await viewModel.SelectTransparencyRangeAsync(range, cancellationToken).ConfigureAwait(true);
  }

  private static View RowTemplate()
  {
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, nameof(ModerationRow.Title));
    var detail = new Label();
    detail.SetBinding(Label.TextProperty, nameof(ModerationRow.Detail));
    return new VerticalStackLayout { Spacing = 3, Children = { title, detail } };
  }
}

internal static class ModerationTransparencyRangeSelection
{
  public static bool ShouldStartLoad(string currentRange, string requestedRange) =>
      currentRange != ModerationTransparencyRange.ParseOrDefault(requestedRange);
}

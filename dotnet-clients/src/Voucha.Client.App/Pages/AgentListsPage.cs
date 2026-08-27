using System.Diagnostics;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public sealed class AgentListsPage : ContentPage
{
  private readonly AgentListsViewModel viewModel;
  private Func<NativeRoutePath, Task>? openPath;
  private CancellationTokenSource? loadCancellation;
  private NativeRoutePath? initialTarget;
  private bool hasAppeared;

  public AgentListsPage(AgentListsViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    var error = new Label { TextColor = Colors.IndianRed };
    error.SetBinding(Label.TextProperty, nameof(AgentListsViewModel.ErrorMessage));
    var retry = UiCopy.Bind(new Button { AutomationId = "agent-retry" }, Button.TextProperty, UiMessageKey.NativeDotnetEngineeringPaginationRetry);
    retry.SetBinding(IsVisibleProperty, nameof(AgentListsViewModel.HasError));
    retry.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    var loading = new ActivityIndicator { AutomationId = "agent-loading" };
    loading.SetBinding(IsVisibleProperty, nameof(AgentListsViewModel.IsLoading));
    loading.SetBinding(ActivityIndicator.IsRunningProperty, nameof(AgentListsViewModel.IsLoading));
    var empty = UiCopy.Bind(new Label { AutomationId = "agent-empty" }, Label.TextProperty, UiMessageKey.NativeSwiftRouteSurfaceNoResults);
    empty.SetBinding(IsVisibleProperty, nameof(AgentListsViewModel.IsEmpty));
    var paginationError = new Label { TextColor = Colors.IndianRed };
    paginationError.SetBinding(Label.TextProperty, nameof(AgentListsViewModel.PaginationErrorMessage));
    var items = new CollectionView { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };
    items.SetBinding(ItemsView.ItemsSourceProperty, nameof(AgentListsViewModel.Rows));
    items.ItemTemplate = new DataTemplate(ItemTemplate);
    var refresh = UiCopy.Bind(
        new Button { AutomationId = "agent-refresh" },
        Button.TextProperty,
        UiMessageKey.NativeDotnetCommonRefresh);
    refresh.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    items.SelectionMode = SelectionMode.Single;
    items.SelectionChanged += async (_, args) =>
    {
      Debug.Assert(openPath is not null, "SetNavigator must be called before the page is visible");
      if (openPath is null || args.CurrentSelection.FirstOrDefault() is not AgentListRow row) return;
      await openPath(row.TargetPath).ConfigureAwait(true);
      items.SelectedItem = null;
    };
    var pagination = new HybridPaginationControl { PaginationId = "agent-lists" };
    pagination.LoadNextPageRequested += OnLoadNextPageRequested;
    pagination.SetBinding(HybridPaginationControl.HasMoreProperty, nameof(AgentListsViewModel.HasMore));
    pagination.SetBinding(HybridPaginationControl.IsLoadingProperty, nameof(AgentListsViewModel.IsLoadingMore));
    pagination.SetBinding(HybridPaginationControl.HasErrorProperty, nameof(AgentListsViewModel.HasPaginationError));
    items.RemainingItemsThreshold = 2;
    items.RemainingItemsThresholdReached += (_, _) => pagination.TryLoadAutomatically();
    Content = new VerticalStackLayout
    {
      Padding = 16,
      Children = { refresh, loading, error, retry, empty, items, paginationError, pagination },
    };
  }

  public void SetContext() =>
      SetDynamicResource(TitleProperty, UiMessageKey.ExtractedIntentsAdminAgents279b44d2.Value);

  public void SetNavigator(Func<NativeRoutePath, Task> navigate) =>
      openPath = navigate ?? throw new ArgumentNullException(nameof(navigate));

  public void SetInitialTarget(NativeRoutePath target) => initialTarget = target;

  public async Task ApplyRouteAsync()
  {
    SetContext();
    hasAppeared = await ReloadAsync().ConfigureAwait(true);
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await LoadOnFirstAppearanceAsync().ConfigureAwait(true);
  }

  internal async Task LoadOnFirstAppearanceAsync()
  {
    if (hasAppeared) return;
    if (await OpenInitialTargetAsync().ConfigureAwait(true)) return;
    hasAppeared = await ReloadAsync().ConfigureAwait(true);
  }

  internal async Task<bool> OpenInitialTargetAsync()
  {
    if (initialTarget is { } target)
    {
      initialTarget = null;
      Debug.Assert(openPath is not null, "SetNavigator must be called before setting an initial target");
      if (openPath is not null) await openPath(target).ConfigureAwait(true);
      return true;
    }
    return false;
  }

  protected override void OnDisappearing()
  {
    CancelPendingLoad();
    base.OnDisappearing();
  }

  internal void CancelPendingLoad() => loadCancellation?.Cancel();

  private async Task<bool> ReloadAsync()
  {
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    var cancellation = new CancellationTokenSource();
    loadCancellation = cancellation;
    await viewModel.LoadAgentsAsync(cancellation.Token).ConfigureAwait(true);
    return !cancellation.IsCancellationRequested;
  }

  private async void OnLoadNextPageRequested(object? sender, EventArgs args) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);

  private static View ItemTemplate()
  {
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, nameof(AgentListRow.Title));
    var detail = new Label();
    detail.SetBinding(Label.TextProperty, nameof(AgentListRow.Detail));
    return new VerticalStackLayout { Padding = new Thickness(0, 6), Children = { title, detail } };
  }
}

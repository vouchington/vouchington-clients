using System.Diagnostics;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public sealed class AgentDetailPage : ContentPage
{
  private readonly AgentListsViewModel viewModel;
  private readonly Entry filterValue = new() { AutomationId = "agent-filter-value" };
  private Func<NativeRoutePath, Task>? openPath;
  private CancellationTokenSource? loadCancellation;
  private string? agentIdOrSlug;
  private bool hasAppeared;

  public AgentDetailPage(AgentListsViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetEngineeringAgentConversationsTitle.Value);

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
    var items = new CollectionView { ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems, SelectionMode = SelectionMode.Single };
    items.SetBinding(ItemsView.ItemsSourceProperty, nameof(AgentListsViewModel.Rows));
    items.ItemTemplate = new DataTemplate(ItemTemplate);
    items.SelectionChanged += async (_, args) =>
    {
      Debug.Assert(openPath is not null, "SetNavigator must be called before the page is visible");
      if (openPath is null || args.CurrentSelection.FirstOrDefault() is not AgentListRow row) return;
      await openPath(row.TargetPath).ConfigureAwait(true);
      items.SelectedItem = null;
    };

    var filterKind = new Picker { AutomationId = "agent-filter-kind" };
    filterKind.SetBinding(Picker.ItemsSourceProperty, nameof(AgentListsViewModel.FilterLabels));
    filterKind.SetBinding(Picker.SelectedIndexProperty, nameof(AgentListsViewModel.SelectedFilterKindIndex), BindingMode.TwoWay);
    filterKind.SetBinding(SemanticProperties.DescriptionProperty, nameof(AgentListsViewModel.FilterKindAccessibilityLabel));
    filterValue.SetBinding(SemanticProperties.DescriptionProperty, nameof(AgentListsViewModel.FilterValueAccessibilityLabel));
    var search = UiCopy.Bind(new Button { AutomationId = "agent-filter-search" }, Button.TextProperty, UiMessageKey.NativeDotnetCsharpSearch);
    search.Clicked += async (_, _) => await viewModel.SearchConversationsAsync(viewModel.SelectedFilterKind, filterValue.Text).ConfigureAwait(true);
    var clear = UiCopy.Bind(new Button { AutomationId = "agent-filter-clear" }, Button.TextProperty, UiMessageKey.NativeSwiftCommonClear);
    clear.Clicked += async (_, _) => { filterValue.Text = string.Empty; await viewModel.ClearConversationSearchAsync().ConfigureAwait(true); };
    var filters = new VerticalStackLayout { AutomationId = "agent-filters", Children = { filterKind, filterValue, search, clear } };

    var detail = new VerticalStackLayout { AutomationId = "agent-detail" };
    detail.Children.Add(UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeSwiftRouteSurfaceAgent));
    detail.Children.Add(BoundLabel(nameof(AgentListsViewModel.AgentDisplayName), UiMessageKey.NativeSwiftRouteSurfaceAgentUser));
    detail.Children.Add(BoundLabel("AgentDetail.Agent.AgentType", UiMessageKey.NativeSwiftRouteSurfaceAgentType));
    detail.Children.Add(BoundLabel("AgentDetail.Agent.Id", UiMessageKey.NativeSwiftRouteSurfaceAgentId));
    detail.Children.Add(BoundLabel("AgentDetail.Agent.SystemUserId", UiMessageKey.NativeSwiftRouteSurfaceAgentSystemUserId));
    detail.Children.Add(BoundLabel(nameof(AgentListsViewModel.AgentCreatedAt), UiMessageKey.NativeSwiftRouteSurfaceCreated));
    var status = new Label { AutomationId = "agent-status" };
    status.SetBinding(Label.TextProperty, nameof(AgentListsViewModel.AgentStatusTitle));
    status.SetBinding(IsVisibleProperty, nameof(AgentListsViewModel.HasAgentStatus));
    detail.Children.Add(status);

    var pagination = new HybridPaginationControl { PaginationId = "agent-lists" };
    pagination.LoadNextPageRequested += OnLoadNextPageRequested;
    pagination.SetBinding(HybridPaginationControl.HasMoreProperty, nameof(AgentListsViewModel.HasMore));
    pagination.SetBinding(HybridPaginationControl.IsLoadingProperty, nameof(AgentListsViewModel.IsLoadingMore));
    pagination.SetBinding(HybridPaginationControl.HasErrorProperty, nameof(AgentListsViewModel.HasPaginationError));
    items.RemainingItemsThreshold = 2;
    items.RemainingItemsThresholdReached += (_, _) => pagination.TryLoadAutomatically();
    var refresh = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCommonRefresh);
    refresh.Clicked += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    var header = new VerticalStackLayout
    {
      AutomationId = "agent-detail-header",
      BindingContext = viewModel,
      Children = { refresh, detail, filters, loading, error, retry, empty },
    };
    items.Header = header;
    var footer = new VerticalStackLayout { Children = { paginationError, pagination } };
    var layout = new Grid
    {
      Padding = 16,
      RowDefinitions =
      {
        new RowDefinition(GridLength.Star),
        new RowDefinition(GridLength.Auto),
      },
    };
    AddAtRow(layout, items, 0);
    AddAtRow(layout, footer, 1);
    Content = layout;
  }

  public void SetContext(string agent, AgentConversationFilter? initialFilter = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(agent);
    viewModel.HydrateConversationFilter(initialFilter);
    filterValue.Text = initialFilter?.Value ?? string.Empty;
    agentIdOrSlug = agent;
  }

  public void SetNavigator(Func<NativeRoutePath, Task> navigate) =>
      openPath = navigate ?? throw new ArgumentNullException(nameof(navigate));

  public bool MatchesContext(string agent) =>
      string.Equals(agentIdOrSlug, agent, StringComparison.Ordinal);

  public async Task ApplyRouteAsync(string agent, AgentConversationFilter? initialFilter = null)
  {
    SetContext(agent, initialFilter);
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
    hasAppeared = await ReloadAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    CancelPendingLoad();
    base.OnDisappearing();
  }

  internal void CancelPendingLoad() => loadCancellation?.Cancel();

  private async Task<bool> ReloadAsync()
  {
    if (agentIdOrSlug is not { } agent) return false;
    loadCancellation?.Cancel();
    loadCancellation?.Dispose();
    var cancellation = new CancellationTokenSource();
    loadCancellation = cancellation;
    await viewModel.LoadConversationsAsync(agent, cancellation.Token).ConfigureAwait(true);
    return !cancellation.IsCancellationRequested;
  }

  private async void OnLoadNextPageRequested(object? sender, EventArgs args) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);

  private static View BoundLabel(string path, UiMessageKey label)
  {
    var value = new Label();
    value.SetBinding(Label.TextProperty, path);
    return new HorizontalStackLayout { Children = { UiCopy.Bind(new Label(), Label.TextProperty, label), value } };
  }

  private static View ItemTemplate()
  {
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, nameof(AgentListRow.Title));
    var detail = new Label();
    detail.SetBinding(Label.TextProperty, nameof(AgentListRow.Detail));
    return new VerticalStackLayout { Padding = new Thickness(0, 6), Children = { title, detail } };
  }

  private static void AddAtRow(Grid grid, View view, int row)
  {
    Grid.SetRow(view, row);
    grid.Add(view);
  }
}

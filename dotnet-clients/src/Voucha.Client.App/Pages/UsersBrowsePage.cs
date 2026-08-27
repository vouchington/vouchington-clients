using System.ComponentModel;
using System.Diagnostics;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Users;

namespace Voucha.Client.App.Pages;

public sealed class UsersBrowsePage : ContentPage, IDisposable
{
  private readonly UsersBrowseViewModel viewModel;
  private readonly IUiLocalization localization;
  private readonly Entry query = UiCopy.Bind(new Entry { AutomationId = "users-browse-query" }, Entry.PlaceholderProperty, UiMessageKey.NativeSwiftMembershipSearchUsers);
  private readonly Label errorMessage = new() { AutomationId = "users-browse-error", TextColor = Colors.IndianRed };
  private readonly Label empty = new() { AutomationId = "users-browse-empty" };
  private readonly Button searchButton;
  private readonly CollectionView results = new() { AutomationId = "users-browse-results" };
  private readonly HybridPaginationControl pagination = new() { PaginationId = "users-browse" };
  private readonly List<Action> rebindRows = [];
  private Func<NativeRoutePath, Task>? openPath;
  private bool disposed;

  public UsersBrowsePage(UsersBrowseViewModel viewModel, IUiLocalization localization)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnChanged;
    query.SetBinding(Entry.TextProperty, nameof(UsersBrowseViewModel.Query), BindingMode.TwoWay);
    searchButton = UiCopy.Bind(new Button { AutomationId = "users-browse-search" }, Button.TextProperty, UiMessageKey.NativeSwiftMembershipSearch);
    searchButton.Clicked += OnSearchClicked;
    UiCopy.Bind(empty, Label.TextProperty, UiMessageKey.NativeSwiftRouteSurfaceNoResults);
    results.ItemTemplate = new DataTemplate(ItemTemplate);
    results.SetBinding(ItemsView.ItemsSourceProperty, nameof(UsersBrowseViewModel.Results));
    results.RemainingItemsThreshold = 2;
    results.RemainingItemsThresholdReached += (_, _) => pagination.TryLoadAutomatically();
    pagination.LoadNextPageRequested += OnLoadMoreClicked;
    pagination.SetBinding(HybridPaginationControl.HasMoreProperty, nameof(UsersBrowseViewModel.HasMoreResults));
    pagination.SetBinding(HybridPaginationControl.IsLoadingProperty, nameof(UsersBrowseViewModel.IsLoadingMoreResults));
    pagination.SetBinding(HybridPaginationControl.HasErrorProperty, nameof(UsersBrowseViewModel.HasMoreResultsError));
    SetDynamicResource(TitleProperty, UiMessageKey.NativeSwiftRouteSurfaceUsers.Value);
    Content = new ScrollView { Content = BuildLayout() };
    Refresh();
  }

  public void SetNavigator(Func<NativeRoutePath, Task> navigate) =>
      openPath = navigate ?? throw new ArgumentNullException(nameof(navigate));

  public Task ApplyRouteAsync(string? initialQuery)
  {
    viewModel.Query = initialQuery ?? string.Empty;
    Refresh();
    return Task.CompletedTask;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (!string.IsNullOrWhiteSpace(viewModel.Query)) await viewModel.SearchAsync().ConfigureAwait(true);
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    viewModel.PropertyChanged -= OnChanged;
  }

  private View BuildLayout() => new VerticalStackLayout
  {
    Padding = 16, Spacing = 10,
    Children = { query, searchButton, results, pagination, empty, errorMessage },
  };

  private View ItemTemplate()
  {
    var name = new Label { FontAttributes = FontAttributes.Bold };
    var handle = new Label();
    var email = new Label { AutomationId = "user-card-email" };
    var status = new Label { AutomationId = "user-card-status" };
    var manage = UiCopy.Bind(new Button { AutomationId = "user-card-manage-link" }, Button.TextProperty, UiMessageKey.NativeSwiftCommonManage);
    var labels = new VerticalStackLayout { Children = { name, handle, email, status } };
    var row = new HorizontalStackLayout { Spacing = 10, Children = { labels, manage } };
    void Bind() => BindRow(row, name, handle, email, status, manage);
    row.BindingContextChanged += (_, _) => Bind();
    rebindRows.Add(Bind);
    manage.Clicked += async (_, _) =>
    {
      Debug.Assert(openPath is not null, "SetNavigator must be called before the page is visible");
      if (openPath is null || row.BindingContext is not UserSearchResult user) return;
      await openPath(UsersBrowseViewModel.AdminTarget(user)).ConfigureAwait(true);
    };
    return row;
  }

  private void BindRow(BindableObject row, Label name, Label handle, Label email, Label status, View manage)
  {
    if (row.BindingContext is not UserSearchResult user) return;
    name.Text = UserLabel(user);
    handle.Text = UiUserHandle.FromUsername(user.Username).Value;
    manage.IsVisible = viewModel.IsAdministrator;
    if (viewModel.IsAdministrator)
    {
      var statusKey = user.SuspendedAt is null
          ? UiMessageKey.NativeSwiftRouteSurfaceUsersBrowseActive
          : UiMessageKey.NativeSwiftRouteSurfaceUsersBrowseSuspended;
      status.IsVisible = true;
      status.Text = localization.Localize(statusKey);
      status.SetDynamicResource(Label.TextProperty, statusKey.Value);
    }
    else
    {
      status.ClearValue(Label.TextProperty);
      status.Text = string.Empty;
      status.IsVisible = false;
    }
    var address = user.EmailAddress;
    email.IsVisible = viewModel.IsAdministrator && !string.IsNullOrWhiteSpace(address);
    email.Text = email.IsVisible ? address : string.Empty;
  }

  private void OnChanged(object? sender, PropertyChangedEventArgs _) => Refresh();

  private async void OnSearchClicked(object? sender, EventArgs args) =>
      await viewModel.SearchAsync().ConfigureAwait(true);

  private async void OnLoadMoreClicked(object? sender, EventArgs args) =>
      await viewModel.LoadMoreResultsAsync().ConfigureAwait(true);

  private void Refresh()
  {
    searchButton.IsEnabled = !viewModel.IsSearching;
    errorMessage.Text = viewModel.SearchError is { } error ? localization.Resolve(error) : string.Empty;
    empty.IsVisible = !viewModel.IsSearching
        && viewModel.SearchError is null
        && viewModel.Results.Count == 0
        && !string.IsNullOrWhiteSpace(viewModel.Query);
    foreach (var bind in rebindRows) bind();
  }

  private static string UserLabel(UserSearchResult user) =>
      string.IsNullOrWhiteSpace(user.Username) ? user.Id : user.Username;
}

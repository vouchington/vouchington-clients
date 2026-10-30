using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public sealed class ListsPageBinding : BindableObject
{
  private readonly ListsViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EventHandler<SessionChangedEventArgs> sessionChangedHandler;
  private readonly SessionChangedSubscription sessionChangedSubscription = new();
  private bool lastStaffViewer;
  private string cachedSelectedListDescription;
  private string cachedSelectedListName;

  public ListsPageBinding(ListsViewModel viewModel, ISessionStore sessionStore)
  {
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    lastStaffViewer = IsStaffViewer;
    sessionChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = RefreshAfterSessionChangedAsync());
    cachedSelectedListDescription = viewModel.SelectedListDescription;
    cachedSelectedListName = viewModel.SelectedListName;
    viewModel.PropertyChanged += (_, e) =>
    {
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.Lists))
      {
        OnPropertyChanged(nameof(Lists));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.Items))
      {
        OnPropertyChanged(nameof(Items));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.HasSelectedList))
      {
        OnPropertyChanged(nameof(HasSelectedList));
        OnPropertyChanged(nameof(CanSaveSelectedList));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.SelectedListName))
      {
        PublishSelectedListNameIfChanged();
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.SelectedListDescription))
      {
        PublishSelectedListDescriptionIfChanged();
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.SelectedList))
      {
        OnPropertyChanged(nameof(SelectedList));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.ErrorMessage))
      {
        OnPropertyChanged(nameof(ErrorMessage));
      }

      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ListsViewModel.HasError))
      {
        OnPropertyChanged(nameof(HasError));
      }

      OnPropertyChanged(nameof(HasMoreLists));
      OnPropertyChanged(nameof(HasMoreItems));
      OnPropertyChanged(nameof(IsLoadingListPage));
      OnPropertyChanged(nameof(IsLoadingItemPage));
      OnPropertyChanged(nameof(HasListPaginationError));
      OnPropertyChanged(nameof(HasItemPaginationError));
    };
  }

  private void PublishSelectedListNameIfChanged()
  {
    if (cachedSelectedListName == viewModel.SelectedListName)
    {
      return;
    }

    cachedSelectedListName = viewModel.SelectedListName;
    OnPropertyChanged(nameof(SelectedListName));
    OnPropertyChanged(nameof(CanSaveSelectedList));
  }

  private void PublishSelectedListDescriptionIfChanged()
  {
    if (cachedSelectedListDescription == viewModel.SelectedListDescription)
    {
      return;
    }

    cachedSelectedListDescription = viewModel.SelectedListDescription;
    OnPropertyChanged(nameof(SelectedListDescription));
  }

  public void AttachSessionChanged()
  {
    if (sessionChangedSubscription.IsAttached) return;
    lastStaffViewer = IsStaffViewer;
    sessionChangedSubscription.TryAttach(sessionStore, sessionChangedHandler);
    OnPropertyChanged(nameof(Lists));
    OnPropertyChanged(nameof(SelectedList));
  }

  public void DetachSessionChanged() =>
      sessionChangedSubscription.TryDetach(sessionStore, sessionChangedHandler);

  private bool IsStaffViewer => sessionStore.Current.Identity?.Roles?.Any(role =>
      role is "administrator" or "moderator") == true;

  internal async Task RefreshAfterSessionChangedAsync()
  {
    var isStaffViewer = IsStaffViewer;
    var gainedStaffAccess = isStaffViewer && !lastStaffViewer;
    lastStaffViewer = isStaffViewer;
    OnPropertyChanged(nameof(Lists));
    OnPropertyChanged(nameof(SelectedList));
    if (sessionChangedSubscription.IsAttached && gainedStaffAccess)
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
  }

  public IReadOnlyList<ListSummaryRow> Lists => viewModel.Lists.Select(row => row with
  {
    IsStaffViewer = IsStaffViewer,
  }).ToArray();

  public IReadOnlyList<ListItemRow> Items => viewModel.Items;

  public bool HasSelectedList => viewModel.HasSelectedList;

  public bool CanSaveSelectedList => HasSelectedList && !string.IsNullOrWhiteSpace(SelectedListName);

  public string SelectedListName
  {
    get => viewModel.SelectedListName;
    set
    {
      if (viewModel.SelectedListName == (value ?? string.Empty))
      {
        return;
      }

      viewModel.SelectedListName = value ?? string.Empty;
    }
  }

  public string SelectedListDescription
  {
    get => viewModel.SelectedListDescription;
    set
    {
      if (viewModel.SelectedListDescription == (value ?? string.Empty))
      {
        return;
      }

      viewModel.SelectedListDescription = value ?? string.Empty;
    }
  }

  public ListSummaryRow? SelectedList => viewModel.SelectedList is { } row
      ? row with { IsStaffViewer = IsStaffViewer }
      : null;

  public string? ErrorMessage => viewModel.ErrorMessage;

  public bool HasError => viewModel.HasError;

  public bool HasMoreLists => viewModel.HasMoreLists;
  public bool HasMoreItems => viewModel.HasMoreItems;
  public bool IsLoadingListPage => viewModel.IsLoadingListPage;
  public bool IsLoadingItemPage => viewModel.IsLoadingItemPage;
  public bool HasListPaginationError => viewModel.HasListPaginationError;
  public bool HasItemPaginationError => viewModel.HasItemPaginationError;
}

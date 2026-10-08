using Voucha.Client.Core.Lists;

namespace Voucha.Client.App.Pages;

public sealed class ListsPageBinding : BindableObject
{
  private readonly ListsViewModel viewModel;
  private string cachedSelectedListDescription;
  private string cachedSelectedListName;

  public ListsPageBinding(ListsViewModel viewModel)
  {
    this.viewModel = viewModel;
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

  public IReadOnlyList<ListSummaryRow> Lists => viewModel.Lists;

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

  public ListSummaryRow? SelectedList => viewModel.SelectedList;

  public string? ErrorMessage => viewModel.ErrorMessage;

  public bool HasError => viewModel.HasError;

  public bool HasMoreLists => viewModel.HasMoreLists;
  public bool HasMoreItems => viewModel.HasMoreItems;
  public bool IsLoadingListPage => viewModel.IsLoadingListPage;
  public bool IsLoadingItemPage => viewModel.IsLoadingItemPage;
  public bool HasListPaginationError => viewModel.HasListPaginationError;
  public bool HasItemPaginationError => viewModel.HasItemPaginationError;
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Lists;

public sealed partial class ListsViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly VouchaApiClient client;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<ListSummaryRow> lists = [];
  private IReadOnlyList<ListItemRow> items = [];
  private ListSummaryRow? selectedList;
  private string selectedListName = string.Empty;
  private string selectedListDescription = string.Empty;
  private ListItemFilter selectedFilter;
  private string? errorMessage;
  private bool isLoading;

  public ListsViewModel(
      VouchaApiClient client,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public IReadOnlyList<ListSummaryRow> Lists
  {
    get => lists;
    private set
    {
      lists = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasLists));
    }
  }

  public IReadOnlyList<ListItemRow> Items
  {
    get => items;
    private set
    {
      items = value;
      OnPropertyChanged();
    }
  }

  public ListSummaryRow? SelectedList
  {
    get => selectedList;
    private set
    {
      selectedList = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasSelectedList));
      SelectedListName = value?.Name ?? string.Empty;
      SelectedListDescription = value?.Description ?? string.Empty;
    }
  }

  public string SelectedListName
  {
    get => selectedListName;
    set
    {
      var nextValue = value ?? string.Empty;
      if (selectedListName == nextValue)
      {
        return;
      }

      selectedListName = nextValue;
      OnPropertyChanged();
    }
  }

  public string SelectedListDescription
  {
    get => selectedListDescription;
    set
    {
      var nextValue = value ?? string.Empty;
      if (selectedListDescription == nextValue)
      {
        return;
      }

      selectedListDescription = nextValue;
      OnPropertyChanged();
    }
  }

  public ListItemFilter SelectedFilter
  {
    get => selectedFilter;
    private set
    {
      selectedFilter = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(IsAllSelected));
      OnPropertyChanged(nameof(IsReadingSelected));
      OnPropertyChanged(nameof(IsWatchSelected));
      OnPropertyChanged(nameof(IsListenSelected));
    }
  }

  public bool IsAllSelected => SelectedFilter == ListItemFilter.All;

  public bool IsReadingSelected => SelectedFilter == ListItemFilter.Reading;

  public bool IsWatchSelected => SelectedFilter == ListItemFilter.Watch;

  public bool IsListenSelected => SelectedFilter == ListItemFilter.Listen;

  public bool HasLists => Lists.Count > 0;

  public bool HasSelectedList => SelectedList is not null;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public bool IsLoading
  {
    get => isLoading;
    private set
    {
      isLoading = value;
      OnPropertyChanged();
    }
  }

  public async Task SelectListAsync(ListSummaryRow list, CancellationToken cancellationToken = default)
  {
    SelectedList = list;
    await LoadSelectedItemsWithErrorStateAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task SelectFilterAsync(ListItemFilter filter, CancellationToken cancellationToken = default)
  {
    SelectedFilter = filter;
    await LoadSelectedItemsWithErrorStateAsync(cancellationToken).ConfigureAwait(true);
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }

}

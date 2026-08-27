using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Tags;

public sealed partial class TagManagementViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly VouchaApiClient client;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private TagManagementRouteContext? context;
  private IReadOnlyList<TagRelationTab> tabs = [];
  private TagRelationTab? selectedTab;
  private IReadOnlyList<TagRelationRow> relations = [];
  private IReadOnlyList<TagSearchResultRow> searchResults = [];
  private IReadOnlyList<PublisherTypeTopic> publisherTypes = [];
  private string title = string.Empty;
  private string? entityLabel;
  private string? errorMessage;
  private string searchQuery = string.Empty;
  private bool isLoading;
  private bool isSearching;

  public TagManagementViewModel(
      VouchaApiClient client,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public TagManagementRouteContext? Context
  {
    get => context;
    private set
    {
      context = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasContext));
    }
  }

  public bool HasContext => Context is not null;

  public string Title
  {
    get => title;
    private set
    {
      if (title == value) return;
      title = value;
      OnPropertyChanged();
    }
  }

  public string? EntityLabel
  {
    get => entityLabel;
    private set
    {
      if (entityLabel == value) return;
      entityLabel = value;
      OnPropertyChanged();
    }
  }

  public IReadOnlyList<TagRelationTab> Tabs
  {
    get => tabs;
    private set
    {
      tabs = value;
      OnPropertyChanged();
    }
  }

  public TagRelationTab? SelectedTab
  {
    get => selectedTab;
    private set
    {
      selectedTab = value;
      OnPropertyChanged();
    }
  }

  public IReadOnlyList<TagRelationRow> Relations
  {
    get => relations;
    private set
    {
      relations = value;
      relationPages.ReplaceItems(value);
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasRelations));
    }
  }

  public bool HasRelations => Relations.Count > 0;

  public IReadOnlyList<TagSearchResultRow> SearchResults
  {
    get => searchResults;
    private set
    {
      searchResults = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasSearchResults));
    }
  }

  public bool HasSearchResults => SearchResults.Count > 0;

  public IReadOnlyList<PublisherTypeTopic> PublisherTypes
  {
    get => publisherTypes;
    private set
    {
      publisherTypes = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasPublisherTypes));
    }
  }

  public bool HasPublisherTypes => PublisherTypes.Count > 0;

  public string SearchQuery
  {
    get => searchQuery;
    set
    {
      var next = value ?? string.Empty;
      if (searchQuery == next) return;
      searchQuery = next;
      OnPropertyChanged();
    }
  }

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

  public bool IsSearching
  {
    get => isSearching;
    private set
    {
      isSearching = value;
      OnPropertyChanged();
    }
  }

  public void SetContext(TagManagementRouteContext next)
  {
    ArgumentNullException.ThrowIfNull(next);
    Context = next;
    Tabs = TabsFor(next);
    SelectedTab = SelectRequestedTab(next.ObjectType);
    ResetRelationPagination();
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}

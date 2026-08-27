using System.ComponentModel;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Bookmarks;

public sealed partial class BookmarkCollectionViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private readonly VouchaApiClient client;
  private readonly ISessionStore sessionStore;
  private readonly IBookmarkService? bookmarkService;
  private readonly HashSet<BookmarkOperationIdentity> pendingActions = [];
  private readonly Lock navigationGate = new();
  private readonly Dictionary<BookmarkOperationIdentity, Lazy<Task<string?>>> pendingDestinations = [];
  private readonly Dictionary<BookmarkOperationIdentity, string> resolvedDestinations = [];
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private BookmarkCollectionRouteContext? context;
  private IReadOnlyList<BookmarkCollectionRow> rows = [];
  private string title = string.Empty;
  private string? errorMessage;
  private UiText? localizedErrorText;
  private bool isLoading;
  private string? mutationErrorMessage;
  private string? navigationErrorMessage;
  private int contextGeneration;
  private int? loadingContextGeneration;

  public BookmarkCollectionViewModel(
      VouchaApiClient client,
      ISessionStore sessionStore,
      IBookmarkService? bookmarkService = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.bookmarkService = bookmarkService;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public BookmarkCollectionRouteContext? Context
  {
    get => context;
    private set
    {
      context = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasContext));
      OnPropertyChanged(nameof(Title));
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

  public IReadOnlyList<BookmarkCollectionRow> Rows
  {
    get => rows;
    private set
    {
      rows = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasRows));
    }
  }

  public bool HasRows => Rows.Count > 0;

  public string? ErrorMessage
  {
    get => localizedErrorText is { } text ? localization.Resolve(text) : errorMessage;
    private set
    {
      localizedErrorText = null;
      errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public string? MutationErrorMessage
  {
    get => mutationErrorMessage;
    private set
    {
      mutationErrorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasMutationError));
    }
  }

  public bool HasMutationError => !string.IsNullOrWhiteSpace(MutationErrorMessage);

  public string? NavigationErrorMessage
  {
    get => navigationErrorMessage;
    private set
    {
      navigationErrorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasNavigationError));
    }
  }

  public bool HasNavigationError => !string.IsNullOrWhiteSpace(NavigationErrorMessage);

  public bool IsLoading
  {
    get => isLoading;
    private set
    {
      isLoading = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanLoadMorePosts));
      OnPropertyChanged(nameof(ShowLoadMore));
      OnPropertyChanged(nameof(CanRetryContinuation));
    }
  }

  public void OnUiLocaleChanged()
  {
    if (Context is not null) Title = localization.Resolve(Context.TitleText);
    OnPropertyChanged(nameof(ErrorMessage));
    Rows = Rows.ToArray();
  }

  public void Dispose() => localeSubscription?.Dispose();

  private void SetLocalizedError(UiText text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
  }
  private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}

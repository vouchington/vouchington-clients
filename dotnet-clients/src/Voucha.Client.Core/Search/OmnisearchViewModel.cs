using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel : INotifyPropertyChanged, IDisposable, IUiLocaleChangeListener
{
  private static readonly string[] OfficialPublicVoteRoles = ["administrator", "investor", "customer_support"];

  private readonly VouchaApiClient client;
  private readonly OmnisearchMode mode;
  private string? fediverseProviders;
  private FediverseProvider selectedFediverseProvider;
  private readonly INavigationViewerProvider? viewerProvider;
  private readonly ISessionStore? sessionStore;
  private readonly SynchronizationContext? syncContext;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private Func<IReadOnlyList<OmnisearchResultGroup>>? remapGroups;
  private CancellationTokenSource? activeSearchCancellation;
  private IReadOnlyList<OmnisearchResultGroup> groups = [];
  private string query = string.Empty;
  private string? errorMessage;
  private bool isLoading;
  private bool shouldSearchInitialQuery;
  private int searchRequestId;

  public OmnisearchViewModel(
      VouchaApiClient client,
      OmnisearchWebSearchRouteContextStore? routeContextStore = null,
      INavigationViewerProvider? viewerProvider = null,
      ISessionStore? sessionStore = null,
      string? initialQuery = null,
      string? fediverseProviders = null,
      OmnisearchMode mode = OmnisearchMode.Combined,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.mode = mode;
    this.fediverseProviders = FediverseProviderFilter(fediverseProviders);
    selectedFediverseProvider = FediverseProviderExtensions.Parse(this.fediverseProviders);
    this.routeContextStore = routeContextStore;
    this.viewerProvider = viewerProvider;
    this.sessionStore = sessionStore;
    this.localization = localization ?? UiLocalization.English;
    syncContext = SynchronizationContext.Current;
    if (viewerProvider is not null)
    {
      viewerProvider.ViewerChanged += OnViewerChanged;
    }
    localeSubscription = localeController?.SubscribeLocaleChanges(this);

    SetInitialQuery(initialQuery);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public string Query
  {
    get => query;
    set
    {
      var next = value ?? string.Empty;
      if (query == next) return;
      query = next;
      OnPropertyChanged();
    }
  }

  public IReadOnlyList<OmnisearchResultGroup> Groups
  {
    get => groups;
    private set
    {
      if (ReferenceEquals(groups, value)) return;
      groups = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasGroups));
    }
  }

  public bool HasGroups => Groups.Count > 0;

  public bool IsLoading
  {
    get => isLoading;
    private set
    {
      if (isLoading == value) return;
      isLoading = value;
      OnPropertyChanged();
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (errorMessage == value) return;
      errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

  public bool SupportsWebSearchNavigation => mode == OmnisearchMode.Combined;

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Native search surfaces API failures in view state before MAUI async handlers observe them.")]
  public async Task SearchAsync(CancellationToken cancellationToken = default)
  {
    var trimmed = (Query ?? string.Empty).Trim();
    var previousCancellation = Interlocked.Exchange(ref activeSearchCancellation, null);
    if (previousCancellation is not null)
    {
      await previousCancellation.CancelAsync().ConfigureAwait(true);
      previousCancellation.Dispose();
    }

    var requestId = ++searchRequestId;
    if (trimmed.Length == 0)
    {
      Groups = [];
      ClearSelectedWebSearchState();
      ErrorMessage = null;
      IsLoading = false;
      return;
    }

    var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    Interlocked.Exchange(ref activeSearchCancellation, requestCancellation);
    ClearSelectedWebSearchState();
    IsLoading = true;
    ErrorMessage = null;

    try
    {
      var groups = mode == OmnisearchMode.Fediverse
          ? await SearchFediverseAsync(trimmed, requestCancellation.Token).ConfigureAwait(true)
          : await SearchCombinedAsync(trimmed, requestCancellation.Token).ConfigureAwait(true);
      if (requestId != searchRequestId) return;
      Groups = groups;
      ClearSelectedWebSearchState();
    }
    catch (OperationCanceledException)
    {
      if (requestId != searchRequestId) return;
      Groups = [];
    }
    catch (Exception ex)
    {
      if (requestId != searchRequestId) return;
      Groups = [];
      ErrorMessage = ex.Message;
    }
    finally
    {
      if (requestId == searchRequestId)
      {
        IsLoading = false;
      }

      if (ReferenceEquals(Interlocked.CompareExchange(ref activeSearchCancellation, null, requestCancellation), requestCancellation))
      {
        requestCancellation.Dispose();
      }
    }
  }

  private bool ViewerIsAuthenticated =>
      viewerProvider?.CurrentViewer.IsAuthenticated ?? false;

  private bool ViewerCanCastPublicVotes =>
      viewerProvider?.CurrentViewer is { IsAuthenticated: true, Roles: var roles } &&
      !OfficialPublicVoteRoles.Any(role => roles.Contains(role, StringComparer.Ordinal));

  private bool ViewerCanTriggerUrlCrawls =>
      viewerProvider?.CurrentViewer is { IsAuthenticated: true, Roles: var roles } &&
      roles.Contains("administrator", StringComparer.Ordinal);

}

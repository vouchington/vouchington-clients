using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ModerationIntegrity;

public abstract partial class IntegrityQueueViewModel<TFlag, TRow> : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  protected IUiLocalization Localization { get; }
  private readonly IDisposable? localeSubscription;
  private readonly HashSet<string> actionIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, UiText> actionErrors = new(StringComparer.Ordinal);
  private readonly HashSet<string> reconciliationIds = new(StringComparer.Ordinal);
  private CancellationTokenSource? loadCancellation;
  private IReadOnlyList<TRow> items = [];
  private IntegrityFlagStatus status = IntegrityFlagStatus.Pending;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private UiText? localizedErrorText;
  private string? cursor;
  private bool hasMore;
  private bool continuationInFlight;
  private int generation;
  protected IntegrityCapabilities Capabilities { get; }

  protected IntegrityQueueViewModel(
      NavigationViewer viewer,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      IntegrityCapabilities? capabilities = null)
  {
    ArgumentNullException.ThrowIfNull(viewer);
    Capabilities = capabilities ?? IntegrityCapabilities.FromViewer(viewer);
    IsAuthorized = Capabilities.CanReviewFlags;
    Localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public bool IsAuthorized { get; }
  public IReadOnlyList<IntegrityFlagStatus> AvailableStatuses { get; } =
      Enum.GetValues<IntegrityFlagStatus>();
  public IntegrityFlagStatus Status => status;
  public IReadOnlyList<TRow> Items
  {
    get => items;
    private set
    {
      if (SetProperty(ref items, value)) OnPropertyChanged(nameof(IsEmpty));
    }
  }
  public LoadState State
  {
    get => state;
    private set
    {
      if (!SetProperty(ref state, value)) return;
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(IsEmpty));
      OnPropertyChanged(nameof(HasError));
    }
  }
  public bool IsLoading => State == LoadState.Loading;
  public bool IsLoadingMore => continuationInFlight;
  public bool IsEmpty => State == LoadState.Loaded && Items.Count == 0;
  public bool HasMore
  {
    get => hasMore;
    private set
    {
      if (SetProperty(ref hasMore, value)) OnPropertyChanged(nameof(CanLoadMore));
    }
  }
  public bool CanLoadMore => HasMore && !IsLoading && !IsLoadingMore;
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
  public string? ErrorMessage
  {
    get => localizedErrorText is { } text ? Localization.Resolve(text) : errorMessage;
    private set
    {
      localizedErrorText = null;
      if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError));
    }
  }

  protected abstract string Id(TFlag flag);
  protected abstract TRow Row(TFlag flag);
  protected abstract bool IsResolved(TFlag flag);
  protected abstract Task<IntegrityQueuePage<TFlag>> FetchAsync(
      IntegrityFlagStatus status, string? after, CancellationToken cancellationToken);

  private IEnumerable<TFlag> Flags() => Items.Select(ItemFlag);
  protected abstract TFlag ItemFlag(TRow row);

  private void AcceptPage(IntegrityQueuePage<TFlag> page, bool append)
  {
    var flags = append
        ? Flags().Concat(page.Results).DistinctBy(Id, StringComparer.Ordinal)
        : page.Results.DistinctBy(Id, StringComparer.Ordinal);
    Items = flags.Select(Row).ToArray();
    cursor = page.EndCursor;
    HasMore = page.HasNextPage;
    ErrorMessage = null;
  }

  private bool EnsureAuthorized()
  {
    if (IsAuthorized) return true;
    SetLocalizedError(UiText.Localized(UiMessageKey.NativeSwiftIntegrityAdministratorMessage));
    State = LoadState.Error;
    return false;
  }

  public void OnUiLocaleChanged()
  {
    Items = Flags().Select(Row).ToArray();
    OnPropertyChanged(nameof(ErrorMessage));
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!disposing) return;
    localeSubscription?.Dispose();
    loadCancellation?.Dispose();
  }

  private void SetLocalizedError(UiText? text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(HasError));
  }

}

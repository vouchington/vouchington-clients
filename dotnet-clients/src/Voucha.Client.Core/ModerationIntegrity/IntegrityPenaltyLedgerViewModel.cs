using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ModerationIntegrity;

public abstract partial class IntegrityPenaltyLedgerViewModel<TPenalty> : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly HashSet<string> actionIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> reconciliationIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, UiText> actionErrors = new(StringComparer.Ordinal);
  private IReadOnlyList<TPenalty> items = [];
  private IntegrityPenaltyStatus status = IntegrityPenaltyStatus.Active;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private UiText? localizedErrorText;
  private string? cursor;
  private bool hasMore;
  private bool continuationInFlight;
  private int generation;
  private readonly IntegrityCapabilities capabilities;

  protected IntegrityPenaltyLedgerViewModel(
      NavigationViewer viewer,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      IntegrityCapabilities? capabilities = null)
  {
    ArgumentNullException.ThrowIfNull(viewer);
    this.capabilities = capabilities ?? IntegrityCapabilities.FromViewer(viewer);
    IsAuthorized = this.capabilities.CanReviewPenalties;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public bool IsAuthorized { get; }
  public IReadOnlyList<IntegrityPenaltyStatus> AvailableStatuses { get; } =
      Enum.GetValues<IntegrityPenaltyStatus>();
  public IntegrityPenaltyStatus Status => status;
  public IReadOnlyList<TPenalty> Items { get => items; private set => SetProperty(ref items, value); }
  public LoadState State { get => state; private set => SetProperty(ref state, value); }
  public string? ErrorMessage
  {
    get => localizedErrorText is { } text ? localization.Resolve(text) : errorMessage;
    private set { localizedErrorText = null; SetProperty(ref errorMessage, value); }
  }
  public bool IsLoading => State == LoadState.Loading;
  public bool IsLoadingMore => continuationInFlight;
  public bool IsEmpty => State == LoadState.Loaded && Items.Count == 0;
  public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
  public bool CanLoadMore => HasMore && !IsLoading && !IsLoadingMore;
  public bool IsUnavailable { get; private set; }

  protected abstract string Id(TPenalty penalty);
  protected abstract bool IsRevoked(TPenalty penalty);
  protected abstract Task<IntegrityQueuePage<TPenalty>> FetchPageAsync(
      IntegrityPenaltyStatus status, string? after, CancellationToken cancellationToken);
  protected abstract Task<TPenalty> FetchExactAsync(
      string penaltyId, CancellationToken cancellationToken);
  protected abstract Task<TPenalty> RevokeExactAsync(
      string penaltyId, CancellationToken cancellationToken);

  public bool IsActionInFlight(string id) => actionIds.Contains(id);
  public bool NeedsReconciliation(string id) => reconciliationIds.Contains(id);
  public string? ActionError(string id) => actionErrors.TryGetValue(id, out var text)
      ? localization.Resolve(text)
      : null;
  public bool CanRevoke(string id) => IsAuthorized && capabilities.CanRevokePenalties &&
      Items.FirstOrDefault(item => Id(item) == id) is { } penalty &&
      !IsRevoked(penalty) && !IsActionInFlight(id) && !NeedsReconciliation(id);

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(Items));
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (disposing) localeSubscription?.Dispose();
  }

  private void SetLocalizedError(UiText? text)
  {
    localizedErrorText = text;
    errorMessage = null;
    OnPropertyChanged(nameof(ErrorMessage));
  }
}

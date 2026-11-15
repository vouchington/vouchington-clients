using System.Net;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ModerationIntegrity;

public abstract partial class IntegrityPenaltyReviewViewModel<TPenalty>
    : ObservableObject, IDisposable, IUiLocaleChangeListener, IIntegrityPenaltyReviewModel
{
  public const int PageSize = 25;

  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly bool canApply;
  private readonly bool canRevoke;
  private readonly HashSet<string> actionIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> reconciliationIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> committedIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> revokedIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, TPenalty> baselines = new(StringComparer.Ordinal);
  private readonly Dictionary<string, UiText> actionErrors = new(StringComparer.Ordinal);
  private IReadOnlyList<TPenalty> items = [];
  private LoadState state = LoadState.Idle;
  private UiText? error;
  private string? cursor;
  private bool hasMore;
  private bool continuationInFlight;
  private bool permissionLost;
  private int generation;

  protected IntegrityPenaltyReviewViewModel(
      bool canApply, bool canRevoke, IUiLocalization? localization, IUiLocaleController? localeController)
  {
    this.canApply = canApply;
    this.canRevoke = canRevoke;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public bool IsAuthorized => canApply && !permissionLost;
  public LoadState State { get => state; private set => SetProperty(ref state, value); }
  public IReadOnlyList<TPenalty> Items { get => items; private set => SetProperty(ref items, value); }
  public abstract IReadOnlyList<IntegrityPenaltyRow> Rows { get; }
  public string? ErrorMessage => error is { } text ? localization.Resolve(text) : null;
  public bool IsEmpty => State == LoadState.Loaded && Items.Count == 0;
  public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
  public bool CanLoadMore => HasMore && IsAuthorized && State == LoadState.Loaded && !continuationInFlight;

  protected abstract string Id(TPenalty penalty);
  protected abstract bool IsRevoked(TPenalty penalty);
  protected abstract DateTimeOffset? ReviewDueAt(TPenalty penalty);
  protected abstract DateTimeOffset? ReviewedAt(TPenalty penalty);
  protected abstract Task<IntegrityQueuePage<TPenalty>> FetchDueAsync(
      string? after, CancellationToken cancellationToken);
  protected abstract Task<TPenalty> ReviewExactAsync(string id, CancellationToken cancellationToken);
  protected abstract Task<TPenalty> FetchExactAsync(string id, CancellationToken cancellationToken);
  protected abstract Task<TPenalty> RevokeExactAsync(string id, CancellationToken cancellationToken);

  public bool IsCommitted(string id) => committedIds.Contains(id);
  public bool NeedsReconciliation(string id) => reconciliationIds.Contains(id);
  public string? ActionError(string id) =>
      actionErrors.TryGetValue(id, out var text) ? localization.Resolve(text) : null;

  public bool CanConfirm(string id) => IsAuthorized && Find(id) is { } penalty && !IsRevoked(penalty) &&
      !committedIds.Contains(id) && !actionIds.Contains(id) && !reconciliationIds.Contains(id);

  public bool CanRevoke(string id) => IsAuthorized && canRevoke && Find(id) is { } penalty &&
      !IsRevoked(penalty) && !actionIds.Contains(id) && !reconciliationIds.Contains(id);

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      State == LoadState.Idle ? RefreshAsync(cancellationToken) : Task.CompletedTask;

  public async Task RefreshAsync(CancellationToken cancellationToken = default)
  {
    if (!EnsureAuthorized()) return;
    var request = ++generation;
    Items = [];
    cursor = null;
    HasMore = false;
    continuationInFlight = false;
    committedIds.Clear();
    SetError(null);
    State = LoadState.Loading;
    try
    {
      var page = await FetchDueAsync(null, cancellationToken).ConfigureAwait(true);
      if (request != generation) return;
      Accept(page, false);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (request == generation) State = LoadState.Idle;
    }
    catch (Exception exception) when (IntegrityMutationFailure.IsExpected(exception) ||
        exception is InvalidOperationException)
    {
      if (request != generation) return;
      SetError(FailureText(exception, UiMessageKey.ModerationPenaltyReviewsLoadFailed));
      State = LoadState.Error;
    }
  }

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (!EnsureAuthorized() || !CanLoadMore) return;
    var request = generation;
    var after = cursor;
    continuationInFlight = true;
    SetError(null);
    try
    {
      var page = await FetchDueAsync(after, cancellationToken).ConfigureAwait(true);
      if (request == generation && after == cursor) Accept(page, true);
    }
    catch (OperationCanceledException) { }
    catch (Exception exception) when (IntegrityMutationFailure.IsExpected(exception) ||
        exception is InvalidOperationException)
    {
      if (request == generation && after == cursor)
        SetError(FailureText(exception, UiMessageKey.ModerationPenaltyReviewsLoadFailed));
    }
    finally
    {
      if (request == generation) continuationInFlight = false;
      Notify();
    }
  }

  public void OnUiLocaleChanged() => Notify();

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (disposing) localeSubscription?.Dispose();
  }

  private TPenalty? Find(string id) => Items.FirstOrDefault(item => Id(item) == id);

  private void Accept(IntegrityQueuePage<TPenalty> page, bool append)
  {
    Items = (append ? Items.Concat(page.Results) : page.Results)
        .Where(item => !revokedIds.Contains(Id(item)))
        .DistinctBy(Id, StringComparer.Ordinal).ToArray();
    cursor = page.EndCursor;
    HasMore = page.HasNextPage;
    Notify();
  }

  private bool EnsureAuthorized()
  {
    if (IsAuthorized) return true;
    SetError(UiText.Localized(UiMessageKey.NativeSwiftIntegrityAdministratorMessage));
    State = LoadState.Error;
    return false;
  }

  private static bool IsForbidden(Exception exception) => exception is HttpRequestException
  {
    StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
  };

  private UiText LosePermission()
  {
    permissionLost = true;
    // Surface at section level: hiding the rows must not hide the only explanation.
    var text = UiText.Localized(UiMessageKey.NativeSwiftIntegrityAdministratorMessage);
    error = text;
    return text;
  }

  private UiText FailureText(Exception exception, UiMessageKey fallback) =>
      IsForbidden(exception) ? LosePermission() : UiText.Localized(fallback);

  private void SetError(UiText? text)
  {
    error = text;
    Notify();
  }

  private void Notify()
  {
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(IsAuthorized));
    OnPropertyChanged(nameof(Items));
    OnPropertyChanged(nameof(Rows));
    OnPropertyChanged(nameof(IsEmpty));
    OnPropertyChanged(nameof(CanLoadMore));
  }
}

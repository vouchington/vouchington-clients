using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ReviewQueueViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IModerationService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly HashSet<string> inFlightPostIds = new(StringComparer.Ordinal);
  private readonly SynchronizationContext? synchronizationContext = SynchronizationContext.Current;
  private IReadOnlyList<ReviewQueueRow> items = [];
  private LoadState state = LoadState.Idle;
  private UiMessageKey? errorMessageKey;
  private string? endCursor;
  private bool hasMore;
  private bool isListLoading;
  private int reconciliationRequired;
  private int listGeneration;
  private long revealContextVersion;
  private TaskCompletionSource? mutationSettlement;

  public IReadOnlyList<ReviewQueueRow> Items
  {
    get => items;
    private set
    {
      if (SetProperty(ref items, value))
      {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsRefreshing));
        OnPropertyChanged(nameof(ShowEmptyState));
      }
    }
  }

  public bool HasItems => Items.Count > 0;
  public bool ShowEmptyState => State == LoadState.Loaded && !HasItems && !HasMore;
  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(ShowEmptyState));
      }
    }
  }
  public bool IsLoading => State == LoadState.Loading;
  public bool IsRefreshing => isListLoading && HasItems;
  public bool HasMore
  {
    get => hasMore;
    private set
    {
      if (SetProperty(ref hasMore, value)) OnPropertyChanged(nameof(ShowEmptyState));
    }
  }
  public string? ErrorMessage => errorMessageKey is { } key ? localization.Localize(key) : null;
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
  public bool RequiresReconciliation => Volatile.Read(ref reconciliationRequired) != 0;
  public bool CanRefresh => !RequiresReconciliation && !isListLoading && inFlightPostIds.Count == 0;
  public bool CanLoadMore => HasMore && endCursor is not null && CanRefresh;

  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      State is LoadState.Idle or LoadState.Error ? ReplaceAsync(cancellationToken) : Task.CompletedTask;

  public async Task ResumeAsync(CancellationToken cancellationToken = default)
  {
    if (!RequiresReconciliation)
    {
      var shouldRefreshExposure = State is not LoadState.Idle and not LoadState.Error
          && isExposureStale;
      await LoadAsync(cancellationToken).ConfigureAwait(true);
      if (shouldRefreshExposure)
      {
        await RefreshExposureAsync(cancellationToken).ConfigureAwait(true);
      }
      return;
    }

    await MutationSettlement.WaitAsync(cancellationToken).ConfigureAwait(true);
    await ReplaceAsync(cancellationToken, reconcilesMutations: true).ConfigureAwait(true);
  }

  public Task ReloadAsync(CancellationToken cancellationToken = default) =>
      RequiresReconciliation ? ResumeAsync(cancellationToken) : RefreshAsync(cancellationToken);

  public Task RefreshAsync(CancellationToken cancellationToken = default) =>
      CanRefresh ? ReplaceAsync(cancellationToken) : Task.CompletedTask;

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      CanLoadMore ? AppendAsync(endCursor!, cancellationToken) : Task.CompletedTask;

  public void CancelListOperations()
  {
    listGeneration = unchecked(listGeneration + 1);
    Interlocked.Increment(ref revealContextVersion);
    SetListLoading(false);
    if (State == LoadState.Loading) State = HasItems ? LoadState.Loaded : LoadState.Idle;
  }

  public Task PerformAsync(ReviewQueueRow row, PostClearanceAction action, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    if (!CanPerform(row, action) || !TryBeginMutation(row.Id)) return Task.CompletedTask;
    return PerformCoreAsync(row.Id, action, cancellationToken);
  }

  public bool CanPerform(ReviewQueueRow row, PostClearanceAction action)
  {
    ArgumentNullException.ThrowIfNull(row);
    var current = Items.FirstOrDefault(item => item.Id == row.Id);
    return !RequiresReconciliation && !isListLoading && !inFlightPostIds.Contains(row.Id) &&
        current is { IsMutating: false } &&
        (action != PostClearanceAction.InReview || current.ClearanceStatus == AdminReviewQueueClearanceStatus.Rejected);
  }

  private void SetListLoading(bool value)
  {
    isListLoading = value;
    OnPropertyChanged(nameof(IsRefreshing));
    OnPropertyChanged(nameof(CanRefresh));
    OnPropertyChanged(nameof(CanLoadMore));
  }

  private int BeginListOperation()
  {
    listGeneration = unchecked(listGeneration + 1);
    SetListLoading(true);
    SetError(null);
    return listGeneration;
  }

  private bool TryBeginMutation(string postId)
  {
    if (inFlightPostIds.Count == 0)
    {
      mutationSettlement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    if (!inFlightPostIds.Add(postId)) return false;
    NotifyMutationAvailabilityChanged();
    return true;
  }

  private void EndMutation(string postId)
  {
    if (!inFlightPostIds.Remove(postId)) return;
    NotifyMutationAvailabilityChanged();
    if (inFlightPostIds.Count != 0) return;
    var settlement = mutationSettlement;
    mutationSettlement = null;
    settlement?.TrySetResult();
  }

  private void NotifyMutationAvailabilityChanged()
  {
    OnPropertyChanged(nameof(CanRefresh));
    OnPropertyChanged(nameof(CanLoadMore));
  }

  private bool Accepts(int generation, CancellationToken cancellationToken) =>
      generation == listGeneration && !cancellationToken.IsCancellationRequested;

  private Task MutationSettlement => mutationSettlement?.Task ?? Task.CompletedTask;

  private void MarkReconciliationRequired()
  {
    if (Interlocked.Exchange(ref reconciliationRequired, 1) != 0) return;
    if (synchronizationContext is not null && SynchronizationContext.Current != synchronizationContext)
    {
      synchronizationContext.Post(static state => ((ReviewQueueViewModel)state!).PublishReconciliationRequired(), this);
      return;
    }
    PublishReconciliationRequired();
  }

  private void PublishReconciliationRequired()
  {
    if (!RequiresReconciliation) return;
    Items = Items.Select(row => row with { RequiresReconciliation = true }).ToArray();
    OnPropertyChanged(nameof(RequiresReconciliation));
    NotifyMutationAvailabilityChanged();
  }

  private void ClearReconciliationRequired()
  {
    if (Interlocked.Exchange(ref reconciliationRequired, 0) == 0) return;
    OnPropertyChanged(nameof(RequiresReconciliation));
    NotifyMutationAvailabilityChanged();
  }

}

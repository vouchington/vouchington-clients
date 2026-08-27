using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ModerationIntegrity;

public abstract partial class IntegrityQueueViewModel<TFlag, TRow>
{
  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      State == LoadState.Idle ? ReloadAsync(cancellationToken) : Task.CompletedTask;

  public async Task SelectStatusAsync(
      IntegrityFlagStatus nextStatus,
      CancellationToken cancellationToken = default)
  {
    if (status == nextStatus && State != LoadState.Idle) return;
    status = nextStatus;
    OnPropertyChanged(nameof(Status));
    await ReloadAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task ReloadAsync(CancellationToken cancellationToken = default)
  {
    if (!EnsureAuthorized()) return;
    var request = ++generation;
    await ReplaceLoadCancellationAsync(cancellationToken).ConfigureAwait(true);
    Items = [];
    cursor = null;
    HasMore = false;
    SetContinuationInFlight(false);
    ErrorMessage = null;
    State = LoadState.Loading;
    try
    {
      var page = await FetchAsync(status, null, loadCancellation!.Token).ConfigureAwait(true);
      if (request != generation) return;
      AcceptPage(page, append: false);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (request == generation) State = LoadState.Idle;
    }
    catch (Exception ex) when (
        ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (request != generation) return;
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (!EnsureAuthorized() || !CanLoadMore) return;
    var request = generation;
    var after = cursor;
    SetContinuationInFlight(true);
    ErrorMessage = null;
    try
    {
      var page = await FetchAsync(status, after, cancellationToken).ConfigureAwait(true);
      if (request != generation || after != cursor) return;
      AcceptPage(page, append: true);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (
        ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (request == generation && after == cursor) ErrorMessage = ex.Message;
    }
    finally
    {
      if (request == generation) SetContinuationInFlight(false);
    }
  }

  private async Task ReplaceLoadCancellationAsync(CancellationToken cancellationToken)
  {
    if (loadCancellation is not null) await loadCancellation.CancelAsync().ConfigureAwait(true);
    loadCancellation?.Dispose();
    loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
  }

  private void SetContinuationInFlight(bool value)
  {
    continuationInFlight = value;
    OnPropertyChanged(nameof(IsLoadingMore));
    OnPropertyChanged(nameof(CanLoadMore));
  }
}

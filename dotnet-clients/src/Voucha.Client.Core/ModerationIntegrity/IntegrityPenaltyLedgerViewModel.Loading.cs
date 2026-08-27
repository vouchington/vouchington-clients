using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public abstract partial class IntegrityPenaltyLedgerViewModel<TPenalty>
{
  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      State == LoadState.Idle ? ReloadAsync(cancellationToken) : Task.CompletedTask;

  public async Task SelectStatusAsync(
      IntegrityPenaltyStatus next, CancellationToken cancellationToken = default)
  {
    if (status == next && State != LoadState.Idle) return;
    status = next;
    OnPropertyChanged(nameof(Status));
    await ReloadAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task ReloadAsync(CancellationToken cancellationToken = default)
  {
    if (!EnsureAuthorized()) return;
    var request = ++generation;
    Items = [];
    cursor = null;
    HasMore = false;
    ErrorMessage = null;
    IsUnavailable = false;
    State = LoadState.Loading;
    try
    {
      var page = await FetchPageAsync(status, null, cancellationToken).ConfigureAwait(true);
      if (request != generation) return;
      Accept(page, false);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (request == generation) State = LoadState.Idle;
    }
    catch (Exception exception) when (IsExpected(exception))
    {
      if (request != generation) return;
      var httpException = exception as HttpRequestException;
      IsUnavailable = httpException?.StatusCode == HttpStatusCode.NotFound;
      ErrorMessage = exception.Message;
      State = LoadState.Error;
    }
  }

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (!EnsureAuthorized() || !CanLoadMore) return;
    var request = generation;
    var after = cursor;
    continuationInFlight = true;
    ErrorMessage = null;
    NotifyLoadingMore();
    try
    {
      var page = await FetchPageAsync(status, after, cancellationToken).ConfigureAwait(true);
      if (request == generation && after == cursor) Accept(page, true);
    }
    catch (OperationCanceledException) { }
    catch (Exception exception) when (IsExpected(exception))
    {
      if (request == generation && after == cursor) ErrorMessage = exception.Message;
    }
    finally
    {
      if (request == generation)
      {
        continuationInFlight = false;
        NotifyLoadingMore();
      }
    }
  }

  private void Accept(IntegrityQueuePage<TPenalty> page, bool append)
  {
    Items = (append ? Items.Concat(page.Results) : page.Results)
        .DistinctBy(Id, StringComparer.Ordinal).ToArray();
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

  private static bool IsExpected(Exception exception) =>
      exception is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException;

  private void NotifyLoadingMore()
  {
    OnPropertyChanged(nameof(IsLoadingMore));
    OnPropertyChanged(nameof(CanLoadMore));
  }
}

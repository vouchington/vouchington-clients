using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      CanAccess && !IsLoading ? LoadPageAsync(replace: true, cancellationToken) : Task.CompletedTask;

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      CanAccess && HasMore && !IsLoading ? LoadPageAsync(replace: false, cancellationToken) : Task.CompletedTask;

  public async Task SelectStatusAsync(ModerationAppealStatus status, CancellationToken cancellationToken = default)
  {
    if (status == SelectedStatus) return;
    _ = Interlocked.Increment(ref loadRevision);
    SelectedStatus = status;
    Appeals = [];
    endCursor = null;
    HasMore = false;
    State = LoadState.Idle;
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  public Task RefreshDeliveryStatusAsync(ModerationAppeal appeal, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return IsDeliveryAmbiguous(appeal) ? RefreshAppealAsync(appeal.Id, cancellationToken) : Task.CompletedTask;
  }

  private async Task LoadPageAsync(bool replace, CancellationToken cancellationToken)
  {
    var revision = Interlocked.Increment(ref loadRevision);
    var expectedStatus = SelectedStatus;
    var cursor = replace ? null : endCursor;
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await service.FetchAppealsAsync(expectedStatus, cursor, cancellationToken: cancellationToken).ConfigureAwait(true);
      if (!IsCurrent(revision, expectedStatus)) return;
      Appeals = replace ? Deduplicate(response.Appeals) : Merge(Appeals, response.Appeals);
      SeedDrafts(response.Appeals);
      endCursor = response.PageInfo.EndCursor;
      HasMore = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      if (IsCurrent(revision, expectedStatus)) State = Appeals.Count == 0 ? LoadState.Idle : LoadState.Loaded;
    }
    catch (OperationCanceledException ex)
    {
      if (!IsCurrent(revision, expectedStatus)) return;
      ErrorMessage = ex.Message;
      State = Appeals.Count == 0 ? LoadState.Error : LoadState.Loaded;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (!IsCurrent(revision, expectedStatus)) return;
      ErrorMessage = ex.Message;
      State = Appeals.Count == 0 ? LoadState.Error : LoadState.Loaded;
    }
  }

  private async Task RefreshAppealAsync(string id, CancellationToken cancellationToken)
  {
    if (IsMutating) return;
    try
    {
      var response = await service.FetchAppealAsync(id, cancellationToken).ConfigureAwait(true);
      ambiguousDeliveryIds.Remove(id);
      Replace(response.Appeal);
      ErrorMessage = null;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException ex)
    {
      ErrorMessage = ex.Message;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      ErrorMessage = ex.Message;
    }
  }

  private bool IsCurrent(int revision, ModerationAppealStatus status) =>
      revision == Volatile.Read(ref loadRevision) && status == SelectedStatus;

  private static ModerationAppeal[] Deduplicate(IEnumerable<ModerationAppeal> source) =>
      source.DistinctBy(appeal => appeal.Id, StringComparer.Ordinal).ToArray();

  private static ModerationAppeal[] Merge(IEnumerable<ModerationAppeal> existing, IEnumerable<ModerationAppeal> additions) =>
      existing.Concat(additions).DistinctBy(appeal => appeal.Id, StringComparer.Ordinal).ToArray();
}

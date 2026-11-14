using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  private readonly HashSet<(int OwnerRevision, string Id)> detailReads = [];

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
    Summaries = [];
    endCursor = null;
    HasMore = false;
    State = LoadState.Idle;
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  public Task RefreshDeliveryStatusAsync(ModerationAppeal appeal, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    var current = Appeals.FirstOrDefault(row => row.Id == appeal.Id);
    return CanAccess && current is not null &&
        IsDeliveryAmbiguous(current)
        ? RefreshAppealAsync(current.Id, cancellationToken) : Task.CompletedTask;
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
      Summaries = replace ? Deduplicate(response.Appeals) : Merge(Summaries, response.Appeals);
      endCursor = response.PageInfo.EndCursor;
      HasMore = response.PageInfo.HasMorePages;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      if (IsCurrent(revision, expectedStatus)) State = Summaries.Count == 0 ? LoadState.Idle : LoadState.Loaded;
    }
    catch (OperationCanceledException ex)
    {
      if (!IsCurrent(revision, expectedStatus)) return;
      ErrorMessage = ex.Message;
      State = Summaries.Count == 0 ? LoadState.Error : LoadState.Loaded;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (!IsCurrent(revision, expectedStatus)) return;
      ErrorMessage = ex.Message;
      State = Summaries.Count == 0 ? LoadState.Error : LoadState.Loaded;
    }
  }

  private async Task RefreshAppealAsync(string id, CancellationToken cancellationToken)
  {
    if (IsMutating) return;
    var original = Appeals.FirstOrDefault(row => row.Id == id);
    if (original is null) return;
    var revision = Volatile.Read(ref loadRevision);
    var ownerRevision = Volatile.Read(ref accountRevision);
    var readKey = (ownerRevision, id);
    if (!detailReads.Add(readKey)) return;
    try
    {
      var response = await service.FetchAppealAsync(id, cancellationToken).ConfigureAwait(true);
      if (!CanAccess || IsMutating || revision != Volatile.Read(ref loadRevision) ||
          ownerRevision != Volatile.Read(ref accountRevision) ||
          !ReferenceEquals(Appeals.FirstOrDefault(row => row.Id == id), original) ||
          response.Appeal.Id != id) return;
      ambiguousDeliveryIds.Remove(id);
      Replace(response.Appeal, preserveUnconfirmedDirtyDraft: true, readOwnerRevision: ownerRevision);
      ErrorMessage = null;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException ex)
    {
      if (CanAccess && ownerRevision == Volatile.Read(ref accountRevision)) ErrorMessage = ex.Message;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (CanAccess && ownerRevision == Volatile.Read(ref accountRevision)) ErrorMessage = ex.Message;
    }
    finally
    {
      detailReads.Remove(readKey);
    }
  }

  private bool IsCurrent(int revision, ModerationAppealStatus status) =>
      revision == Volatile.Read(ref loadRevision) && status == SelectedStatus;

  private static ModerationAppealSummary[] Deduplicate(IEnumerable<ModerationAppealSummary> source) =>
      source.DistinctBy(appeal => appeal.Id, StringComparer.Ordinal).ToArray();

  private static ModerationAppealSummary[] Merge(IEnumerable<ModerationAppealSummary> existing, IEnumerable<ModerationAppealSummary> additions) =>
      existing.Concat(additions).DistinctBy(appeal => appeal.Id, StringComparer.Ordinal).ToArray();
}

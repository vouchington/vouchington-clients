using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationDisputesViewModel
{
  public Task LoadAsync(CancellationToken cancellationToken = default) =>
      CanAccess && !IsLoading
          ? LoadPageAsync(replace: true, cancellationToken)
          : Task.CompletedTask;

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      CanAccess && HasMore && !IsLoading
          ? LoadPageAsync(replace: false, cancellationToken)
          : Task.CompletedTask;

  public async Task SelectStatusAsync(
      ModerationDisputeStatus status,
      CancellationToken cancellationToken = default)
  {
    if (status == SelectedStatus) return;
    _ = Interlocked.Increment(ref loadRevision);
    SelectedStatus = status;
    Disputes = [];
    endCursor = null;
    HasMore = false;
    State = LoadState.Idle;
    SetError(null);
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task RefreshAmbiguousAsync(
      string disputeId,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(disputeId);
    if (!CanAccess || IsMutating || !ambiguousMutationIds.Contains(disputeId)) return;
    _ = await MutateAsync(disputeId, async () =>
    {
      var response = await service.FetchDisputeAsync(
          disputeId,
          cancellationToken).ConfigureAwait(true);
      if (!HasRerunReconciled(response.Dispute)) return;
      UnlockRerun(disputeId);
      Replace(response.Dispute, preserveUnconfirmedDirtyDraft: true);
    }, cancellationToken).ConfigureAwait(true);
  }

  private async Task LoadPageAsync(bool replace, CancellationToken cancellationToken)
  {
    var revision = Interlocked.Increment(ref loadRevision);
    var expectedMutationOutcomeRevision = Volatile.Read(ref mutationOutcomeRevision);
    var expectedStatus = SelectedStatus;
    var cursor = replace ? null : endCursor;
    State = LoadState.Loading;
    SetError(null);
    try
    {
      var response = await service.FetchDisputesAsync(
          expectedStatus,
          cursor,
          cancellationToken: cancellationToken).ConfigureAwait(true);
      if (!IsCurrent(revision, expectedMutationOutcomeRevision, expectedStatus)) return;
      Disputes = replace
          ? Deduplicate(response.Disputes)
          : Merge(Disputes, response.Disputes);
      SeedDrafts(response.Disputes);
      endCursor = response.PageInfo.EndCursor;
      HasMore = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      if (IsCurrent(revision, expectedMutationOutcomeRevision, expectedStatus))
        State = Disputes.Count == 0 ? LoadState.Idle : LoadState.Loaded;
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      if (!IsCurrent(revision, expectedMutationOutcomeRevision, expectedStatus)) return;
      SetError(UiMessageKey.NativeSwiftModerationReportsActionFailed);
      State = Disputes.Count == 0 ? LoadState.Error : LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (!IsCurrent(revision, expectedMutationOutcomeRevision, expectedStatus)) return;
      SetError(UiMessageKey.NativeSwiftModerationReportsActionFailed);
      State = Disputes.Count == 0 ? LoadState.Error : LoadState.Loaded;
    }
  }

  private bool IsCurrent(
      int revision,
      int expectedMutationOutcomeRevision,
      ModerationDisputeStatus status) =>
      revision == Volatile.Read(ref loadRevision) &&
      expectedMutationOutcomeRevision == Volatile.Read(ref mutationOutcomeRevision) &&
      status == SelectedStatus;

  private static ModerationDispute[] Deduplicate(
      IEnumerable<ModerationDispute> source) =>
      source.DistinctBy(dispute => dispute.Id, StringComparer.Ordinal).ToArray();

  private static ModerationDispute[] Merge(
      IEnumerable<ModerationDispute> existing,
      IEnumerable<ModerationDispute> additions) =>
      existing.Concat(additions)
          .DistinctBy(dispute => dispute.Id, StringComparer.Ordinal)
          .ToArray();
}

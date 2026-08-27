using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public sealed partial class RewardsProgramStatusesViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Create failures preserve the current surface.")]
  public async Task CreateAsync(RewardsProgramStatusOptionRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    if (IsCreating) return;
    IsCreating = true;
    SetError(null);
    try
    {
      var created = await service.CreateAsync(new CreateRewardsProgramStatusBody(row.Value.Id), cancellationToken).ConfigureAwait(true);
      TrackLocalUpsert(created);
      pendingDeletions.Remove(created.Id);
      Replace([.. statuses, created]);
      SetSearchRows([]);
      SearchQuery = string.Empty;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception error) { Fail(error); }
    finally { IsCreating = false; }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Failed update preserves its draft.")]
  public async Task SaveAsync(CancellationToken cancellationToken = default)
  {
    if (EditDraft is not { Original: { } original } draft || mutatingIds.Contains(original.Id)) return;
    if (!draft.TryBuildUpdate(out var body))
    {
      SetError(UiText.Localized(UiMessageKey.ExtractedMyRewardsProgramStatusesManagerSinceMustBeBeforeUntilA4745364));
      return;
    }
    if (body is null) { mutatingIds.Remove(original.Id); CancelEdit(); return; }
    mutatingIds.Add(original.Id);
    RefreshRows();
    OnPropertyChanged(nameof(CanSave));
    SetError(null);
    try
    {
      var updated = await service.UpdateAsync(original.Id, body, cancellationToken).ConfigureAwait(true);
      TrackLocalUpsert(updated);
      Replace(statuses.Select(value => value.Id == updated.Id ? updated : value));
      if (ReferenceEquals(EditDraft, draft)) CancelEdit();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception error) { Fail(error); }
    finally { mutatingIds.Remove(original.Id); RefreshRows(); OnPropertyChanged(nameof(CanSave)); }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Optimistic delete must restore on failure.")]
  public async Task DeleteAsync(RewardsProgramStatusRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    var rank = Array.FindIndex(statuses, value => value.Id == row.Id);
    if (rank < 0 || !mutatingIds.Add(row.Id)) return;
    var hadLocal = localUpserts.Remove(row.Id, out var previousLocal);
    pendingDeletions[row.Id] = new([.. inFlightReadIds]);
    Replace(statuses.Where(value => value.Id != row.Id));
    SetError(null);
    try
    {
      await service.DeleteAsync(row.Id, cancellationToken).ConfigureAwait(true);
      CompleteLocalDeletion(row.Id);
      if (EditDraft?.Original?.Id == row.Id) CancelEdit();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Restore(); }
    catch (Exception error) { Restore(); Fail(error); }
    finally { mutatingIds.Remove(row.Id); RefreshRows(); }

    void Restore()
    {
      pendingDeletions.Remove(row.Id);
      if (hadLocal && previousLocal is not null) localUpserts[row.Id] = previousLocal;
      var restored = statuses.Where(value => value.Id != row.Id).ToList();
      restored.Insert(Math.Min(rank, restored.Count), row.Value);
      Replace(restored);
    }
  }

  private void TrackLocalUpsert(RewardsProgramStatus value)
  {
    if (inFlightReadIds.Count == 0) { localUpserts.Remove(value.Id); return; }
    localUpserts[value.Id] = new(value, [.. inFlightReadIds]);
  }
  private void CompleteLocalDeletion(string id)
  {
    if (!pendingDeletions.TryGetValue(id, out var deletion)) return;
    deletion.MutationPending = false;
    if (deletion.PendingReadIds.Count == 0) pendingDeletions.Remove(id);
  }
  private void RefreshRows() => Replace(statuses);
}

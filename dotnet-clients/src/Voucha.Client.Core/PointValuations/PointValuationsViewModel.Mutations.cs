using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PointValuations;

public sealed partial class PointValuationsViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Create failures preserve drafts.")]
  public async Task CreateAsync(RewardsProgramRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    if (IsCreating || valuations.Any(value => value.RewardsProgramId == row.Value.Id)) return;
    if (!CreateDraft.TryBuildCreate(row.Value.Id, out var body))
    {
      SetError(UiText.Localized(UiMessageKey.ExtractedMyPointValuationsManagerPleaseEnterAvalidValuePerPoint67b38bc3));
      return;
    }
    IsCreating = true; SetError(null);
    try
    {
      var created = await service.CreateAsync(body!, cancellationToken).ConfigureAwait(true);
      TrackLocalUpsert(created); pendingDeletions.Remove(created.Id);
      Replace([.. valuations, created]);
      CreateDraft = new(localization.Culture); OnPropertyChanged(nameof(CreateDraft));
      SetSearchResults([]); SearchQuery = string.Empty;
    }
    catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    { Fail(error); }
    finally { IsCreating = false; }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Update failures preserve drafts.")]
  public async Task SaveAsync(CancellationToken cancellationToken = default)
  {
    if (EditDraft is not { Original: { } original } draft || mutatingIds.Contains(original.Id)) return;
    if (!draft.TryBuildUpdate(out var body))
    {
      SetError(UiText.Localized(UiMessageKey.ExtractedMyPointValuationsManagerPleaseEnterAvalidValuePerPoint67b38bc3));
      return;
    }
    if (body is null) { CancelEdit(); SetError(null); return; }
    mutatingIds.Add(original.Id); SetError(null);
    try
    {
      var updated = await service.UpdateAsync(original.Id, body, cancellationToken).ConfigureAwait(true);
      TrackLocalUpsert(updated);
      Replace(valuations.Select(value => value.Id == updated.Id ? updated : value));
      CancelEdit();
    }
    catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    { Fail(error); }
    finally { mutatingIds.Remove(original.Id); }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Delete rollback restores presentation state.")]
  public async Task DeleteAsync(PointValuationRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    var rank = Array.FindIndex(valuations, value => value.Id == row.Id);
    if (rank < 0 || !mutatingIds.Add(row.Id)) return;
    var hadLocal = localUpserts.Remove(row.Id, out var previousLocal);
    pendingDeletions[row.Id] = new([.. inFlightReadIds]);
    Replace(valuations.Where(value => value.Id != row.Id)); SetError(null);
    try
    {
      await service.DeleteAsync(row.Id, cancellationToken).ConfigureAwait(true);
      CompleteLocalDeletion(row.Id);
      if (EditDraft?.Original?.Id == row.Id) CancelEdit();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      RestoreDeletedValuation(row, rank, hadLocal, previousLocal);
    }
    catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
      RestoreDeletedValuation(row, rank, hadLocal, previousLocal);
      Fail(error);
    }
    finally { mutatingIds.Remove(row.Id); }
  }

  private void RestoreDeletedValuation(
      PointValuationRow row, int rank, bool hadLocal, PendingLocalUpsert? previousLocal)
  {
    pendingDeletions.Remove(row.Id);
    if (hadLocal && previousLocal is not null)
    {
      previousLocal.PendingReadIds.IntersectWith(inFlightReadIds);
      if (previousLocal.PendingReadIds.Count > 0) localUpserts[row.Id] = previousLocal;
    }
    var restored = valuations.Where(value => value.Id != row.Id).ToList();
    restored.Insert(Math.Min(rank, restored.Count), row.Value);
    Replace(restored);
  }

  private void TrackLocalUpsert(PointValuation value)
  {
    if (inFlightReadIds.Count == 0)
    {
      localUpserts.Remove(value.Id);
      return;
    }
    localUpserts[value.Id] = new(value, [.. inFlightReadIds]);
  }

  private void CompleteLocalDeletion(string id)
  {
    if (!pendingDeletions.TryGetValue(id, out var deletion)) return;
    deletion.MutationPending = false;
    if (deletion.PendingReadIds.Count == 0) pendingDeletions.Remove(id);
  }
}

using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.SpendingCategories;

public sealed partial class SpendingCategoriesViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Create failures preserve drafts.")]
  public async Task CreateAsync(SpendingCategoryOptionRow row, CancellationToken token = default)
  {
    ArgumentNullException.ThrowIfNull(row); if (IsCreating) return;
    if (!CreateDraft.TryBuildCreate(row.Value.Id, out var body)) { SetError(UiText.Localized(UiMessageKey.ExtractedMySpendingCategoriesManagerPleaseEnterAvalidAmount010f1dd6)); return; }
    IsCreating = true; SetError(null);
    try { var created = await service.CreateAsync(body!, token).ConfigureAwait(true); TrackLocalUpsert(created); pendingDeletions.Remove(created.Id); Replace([.. values, created]); CreateDraft = new(localization.Culture); Notify(nameof(CreateDraft)); SearchQuery = string.Empty; }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception error) { Fail(error); }
    finally { IsCreating = false; }
  }
  public void BeginEdit(SpendingCategoryRow row) { ArgumentNullException.ThrowIfNull(row); if (!row.CanManage) return; EditDraft = new(row.Value, localization.Culture); Notify(nameof(HasEditDraft)); }
  public void CancelEdit() { EditDraft = null; Notify(nameof(HasEditDraft)); }
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Update failures preserve drafts.")]
  public async Task SaveAsync(CancellationToken token = default)
  {
    if (EditDraft?.Original is not { CanManage: true } original || !mutating.Add(original.Id)) return;
    var draft = EditDraft;
    if (!draft.TryBuildUpdate(out var body)) { mutating.Remove(original.Id); SetError(UiText.Localized(UiMessageKey.ExtractedMySpendingCategoriesManagerPleaseEnterAvalidAmount010f1dd6)); return; }
    if (body is null) { mutating.Remove(original.Id); CancelEdit(); SetError(null); return; }
    SetError(null);
    try { var updated = await service.UpdateAsync(original.Id, body, token).ConfigureAwait(true); TrackLocalUpsert(updated); Replace(values.Select(value => value.Id == updated.Id ? updated : value)); CancelEdit(); }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception error) { Fail(error); }
    finally { mutating.Remove(original.Id); }
  }
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Delete rollback restores presentation state.")]
  public async Task DeleteAsync(SpendingCategoryRow row, CancellationToken token = default)
  {
    ArgumentNullException.ThrowIfNull(row); var rank = Array.FindIndex(values, value => value.Id == row.Id);
    if (!row.CanManage || rank < 0 || !mutating.Add(row.Id)) return;
    var hadLocal = localUpserts.Remove(row.Id, out var previousLocal); pendingDeletions[row.Id] = new([.. inFlightReadIds]); Replace(values.Where(value => value.Id != row.Id)); SetError(null);
    try { await service.DeleteAsync(row.Id, token).ConfigureAwait(true); CompleteLocalDeletion(row.Id); if (EditDraft?.Original?.Id == row.Id) CancelEdit(); }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { RestoreDeleted(row, rank, hadLocal, previousLocal); }
    catch (Exception error) { RestoreDeleted(row, rank, hadLocal, previousLocal); Fail(error); }
    finally { mutating.Remove(row.Id); }
  }
}

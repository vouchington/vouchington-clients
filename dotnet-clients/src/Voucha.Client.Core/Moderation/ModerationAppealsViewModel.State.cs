using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  private void SeedDrafts(IEnumerable<ModerationAppeal> loaded)
  {
    foreach (var appeal in loaded)
    {
      if (!dirtyDraftIds.Contains(appeal.Id)) drafts[appeal.Id] = ServerDraftFor(appeal);
    }
  }

  private void Replace(ModerationAppeal appeal, bool preserveUnconfirmedDirtyDraft = false)
  {
    if (!CanAccess || activeMutationOwnerRevision != Volatile.Read(ref accountRevision)) return;
    ReconcileCurrentMutation(appeal, activeMutationAdmissionToken);
    var editedDraft = dirtyDraftIds.Contains(appeal.Id) ? DraftFor(appeal) : null;
    ConfirmServerDraft(appeal);
    if (preserveUnconfirmedDirtyDraft && editedDraft is not null &&
        !string.Equals(editedDraft, appeal.PublicResponse, StringComparison.Ordinal))
    {
      drafts[appeal.Id] = editedDraft;
      dirtyDraftIds.Add(appeal.Id);
    }
    var updated = Appeals.ToList();
    var index = updated.FindIndex(item => item.Id == appeal.Id);
    if (appeal.Status != SelectedStatus)
    {
      if (index >= 0) updated.RemoveAt(index);
    }
    else if (index >= 0)
    {
      updated[index] = appeal;
    }
    else
    {
      updated.Insert(0, appeal);
    }
    Appeals = updated;
  }

  private void ConfirmServerDraft(ModerationAppeal appeal)
  {
    drafts[appeal.Id] = ServerDraftFor(appeal);
    dirtyDraftIds.Remove(appeal.Id);
  }

}

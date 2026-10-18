using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  public bool IsAIRerunPending(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return CanAccess && admissionStore.Contains(admissionOwner, appeal.Id);
  }

  public bool CanRefreshAIRerun(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    return CanAccess && !IsMutating && appeal.Status == ModerationAppealStatus.Pending &&
        Appeals.Any(item => item.Id == appeal.Id) && IsAIRerunPending(appeal);
  }

  public Task RefreshPendingAIRerunAsync(
      ModerationAppeal appeal,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanRefreshAIRerun(appeal) || !admissionStore.TryGet(admissionOwner, appeal.Id, out var admission))
      return Task.CompletedTask;
    var revision = Volatile.Read(ref loadRevision);
    var ownerRevision = Volatile.Read(ref accountRevision);
    return AwaitMutationAsync(appeal.Id, async () =>
    {
      var refreshed = (await service.FetchAppealAsync(appeal.Id, cancellationToken)
          .ConfigureAwait(true)).Appeal;
      if (!IsCurrentAIRerun(appeal.Id, admission, revision, ownerRevision, refreshed)) return;
      if (!admission.IsReconciled(refreshed))
        throw new TimeoutException(localization.Localize(
            UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing));
      ClearPendingAIRerun(appeal.Id, admission.Token);
      Replace(refreshed, preserveUnconfirmedDirtyDraft: true);
    }, cancellationToken);
  }

  private bool IsCurrentAIRerun(
      string appealId,
      AppealAIRerunAdmissionStore.Admission admission,
      int revision,
      int ownerRevision,
      ModerationAppeal refreshed) =>
      refreshed.Id == appealId &&
      IsCurrentAIRerunContext(appealId, admission, revision, ownerRevision);

  private bool IsCurrentAIRerunContext(
      string appealId,
      AppealAIRerunAdmissionStore.Admission admission,
      int revision,
      int ownerRevision) =>
      CanAccess && ownerRevision == Volatile.Read(ref accountRevision) &&
      revision == Volatile.Read(ref loadRevision) &&
      SelectedStatus == ModerationAppealStatus.Pending &&
      Appeals.Any(item => item.Id == appealId) &&
      admissionStore.TryGet(admissionOwner, appealId, out var current) && current == admission;

  private void ReconcileAIReruns(IEnumerable<ModerationAppeal> loaded)
  {
    admissionStore.Reconcile(admissionOwner, loaded);
    OnPropertyChanged(nameof(Appeals));
  }

  private bool TrySetPendingAIRerun(
      ModerationAppeal appeal,
      out AppealAIRerunAdmissionStore.Admission admission)
  {
    if (!admissionStore.TryAdmit(admissionOwner, appeal, out admission)) return false;
    OnPropertyChanged(nameof(Appeals));
    return true;
  }

  private void ClearPendingAIRerun(string appealId, Guid token)
  {
    admissionStore.Remove(admissionOwner, appealId, token);
    OnPropertyChanged(nameof(Appeals));
  }

  private void OnViewerChanged(object? sender, NavigationViewerChangedEventArgs args)
  {
    if (viewer.HasSameIdentityAccess(args.Viewer)) return;
    _ = Interlocked.Increment(ref accountRevision);
    _ = Interlocked.Increment(ref loadRevision);
    drafts.Clear();
    dirtyDraftIds.Clear();
    ambiguousDeliveryIds.Clear();
    Appeals = [];
    State = LoadState.Idle;
    ErrorMessage = null;
    OnPropertyChanged(nameof(Appeals));
  }

}

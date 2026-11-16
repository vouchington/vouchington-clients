using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationDisputesViewModel
{
  /// <summary>
  /// Status copy for the exact AI draft run staff requested. While a re-run is pending, a
  /// server-reported status only applies when the dispute names the queued run, so a stale row
  /// never borrows an older run's outcome. A succeeded run has no copy.
  /// </summary>
  public UiMessageKey? AIDraftStatusKey(ModerationDispute dispute)
  {
    ArgumentNullException.ThrowIfNull(dispute);
    if (!CanAccess) return null;
    if (rerunBaselines.TryGetValue(dispute.Id, out var pending))
    {
      var exact = pending.RunId is not null &&
          string.Equals(dispute.AiDraftRequestedRunId, pending.RunId, StringComparison.Ordinal);
      return exact && dispute.AiDraftRequestedRunStatus is { } status
          ? KeyFor(status)
          : KeyFor(ModerationRunStatus.Active);
    }
    return dispute.AiDraftRequestedRunStatus is { } reported ? KeyFor(reported) : null;
  }

  private void NoteQueuedRun(string disputeId, string runId)
  {
    if (rerunBaselines.TryGetValue(disputeId, out var baseline))
      rerunBaselines[disputeId] = baseline with { RunId = runId };
    OnPropertyChanged(nameof(Disputes));
  }

  // A failed or expired exact run is recorded on the visible row before the pending re-run is
  // released, so its status copy never falls back to the previous run's snapshot.
  private bool EndObservedRun(ModerationDispute refreshed)
  {
    if (!rerunBaselines.TryGetValue(refreshed.Id, out var pending) || pending.RunId is null ||
        !string.Equals(refreshed.AiDraftRequestedRunId, pending.RunId, StringComparison.Ordinal) ||
        refreshed.AiDraftRequestedRunStatus is not (ModerationRunStatus.Failed or ModerationRunStatus.Expired))
      return false;
    Replace(refreshed, preserveUnconfirmedDirtyDraft: true);
    UnlockRerun(refreshed.Id);
    SetError(KeyFor(refreshed.AiDraftRequestedRunStatus.Value));
    return true;
  }

  private static UiMessageKey? KeyFor(ModerationRunStatus status) => status switch
  {
    ModerationRunStatus.Failed => UiMessageKey.NativeSwiftReviewDisputesRerunFailed,
    ModerationRunStatus.Expired => UiMessageKey.NativeSwiftReviewDisputesRerunExpired,
    ModerationRunStatus.Active => UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing,
    _ => null,
  };
}

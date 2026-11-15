using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationAppealsViewModel
{
  /// <summary>
  /// Status copy for the exact AI draft run staff requested. A pending admission whose run has not
  /// been read yet stays "active"; a server-reported status only applies when the appeal names the
  /// admitted run, so a newer request never borrows an older run's outcome.
  /// </summary>
  public UiMessageKey? AIDraftStatusKey(ModerationAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);
    if (!CanAccess) return null;
    if (admissionStore.TryGet(admissionOwner, appeal.Id, out var admission))
    {
      var exact = admission.RunId is not null &&
          string.Equals(appeal.AiDraftRequestedRunId, admission.RunId, StringComparison.Ordinal);
      return exact && appeal.AiDraftRequestedRunStatus is { } status
          ? KeyFor(status)
          : UiMessageKey.NativeSwiftAiRunStatusAppealDraftActive;
    }
    return appeal.AiDraftRequestedRunStatus is { } reported ? KeyFor(reported) : null;
  }

  private static UiMessageKey KeyFor(ModerationRunStatus status) => status switch
  {
    ModerationRunStatus.Succeeded => UiMessageKey.NativeSwiftAiRunStatusAppealDraftSucceeded,
    ModerationRunStatus.Failed => UiMessageKey.NativeSwiftAiRunStatusAppealDraftFailed,
    ModerationRunStatus.Expired => UiMessageKey.NativeSwiftAiRunStatusAppealDraftExpired,
    _ => UiMessageKey.NativeSwiftAiRunStatusAppealDraftActive,
  };
}

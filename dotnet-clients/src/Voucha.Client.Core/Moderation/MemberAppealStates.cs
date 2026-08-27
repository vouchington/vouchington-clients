using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed record MemberAppealPaginationState(
    bool HasMore,
    bool IsLoading,
    bool HasError);

public enum MemberAppealSubmissionOutcome
{
  Idle,
  Submitting,
  Submitted,
  Duplicate,
  ReasonRequired,
  DetailsRequired,
  DetailsTooLong,
  Failed,
}

public static class MemberAppealSubmissionMessages
{
  public static UiMessageKey? MessageKey(MemberAppealSubmissionOutcome outcome) =>
      outcome switch
      {
        MemberAppealSubmissionOutcome.Submitted =>
            UiMessageKey.NativeSwiftModerationAppealsMemberSubmitted,
        MemberAppealSubmissionOutcome.Duplicate =>
            UiMessageKey.NativeSwiftModerationAppealsMemberPendingExists,
        MemberAppealSubmissionOutcome.ReasonRequired =>
            UiMessageKey.NativeSwiftModerationAppealsMemberReasonRequired,
        MemberAppealSubmissionOutcome.DetailsRequired =>
            UiMessageKey.NativeSwiftModerationAppealsMemberDetailsRequired,
        MemberAppealSubmissionOutcome.DetailsTooLong =>
            UiMessageKey.NativeSwiftModerationAppealsMemberDetailsTooLong,
        MemberAppealSubmissionOutcome.Failed =>
            UiMessageKey.NativeSwiftModerationAppealsMemberGenericError,
        _ => null,
      };
}

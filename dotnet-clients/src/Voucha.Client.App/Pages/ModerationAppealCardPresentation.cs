using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal static partial class ModerationAppealCardPresentation
{
  public static string Heading(ModerationAppeal appeal) =>
      UiCopy.Format(
          UiMessageKey.NativeSwiftModerationAppealsAppealId,
          ("id", UiText.ProtocolValue(appeal.CaseId ?? appeal.Id)));

  public static string Reason(ModerationAppeal appeal) =>
      appeal.AppealReason ??
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsNoReason);

  public static string Recommendation(ModerationAppeal appeal) =>
      appeal.RecommendedAction is { } action
          ? UiCopy.Format(
              UiMessageKey.NativeSwiftModerationAppealsAiRecommendation,
              ("recommendation", Action(action)))
          : UiCopy.Localize(UiMessageKey.NativeDotnetModerationNoRecommendation);

  public static string LabelValue(UiMessageKey label, string value) =>
      UiCopy.Format(
          UiMessageKey.NativeSwiftModerationAppealsLabelValue,
          ("label", UiText.Localized(label)),
          ("value", UiText.UserContent(value)));

  public static string Context(ModerationAppeal appeal)
  {
    var target = Target(appeal);
    var appellant = appeal.StaffContext is { } staff
        ? UiText.UserContent(ActorLabel(staff.Appellant))
        : appeal.AppellantId is { } appellantId
            ? UiText.ProtocolValue(appellantId)
            : UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsUnknown);
    var lines = new List<string>
    {
      UiCopy.Format(
          UiMessageKey.NativeSwiftModerationAppealsAppellantContext,
          ("appellant", appellant),
          ("target", target),
          ("status", Status(appeal.Status))),
    };
    AddTargetContext(lines, appeal.TargetContext);
    if (appeal.StaffContext is { } staffContext)
    {
      AddValue(
          lines,
          UiMessageKey.NativeSwiftModerationAppealsAppellantId,
          "id",
          ActorLabel(staffContext.Appellant));
      AddValue(
          lines,
          UiMessageKey.NativeSwiftModerationAppealsOriginalDecisionReason,
          "value",
          staffContext.OriginalDecision.InternalReason);
      if (staffContext.OriginalDecision.Actor is { } actor)
        AddValue(
            lines,
            UiMessageKey.NativeSwiftModerationAppealsOriginalDecisionActor,
            "value",
            ActorLabel(actor));
    }
    return string.Join(Environment.NewLine, lines);
  }

  public static string Lifecycle(ModerationAppeal appeal)
  {
    var lines = new List<string>
    {
      Event(UiMessageKey.NativeSwiftModerationAppealsCreated, appeal.CreatedAt),
      Event(UiMessageKey.NativeSwiftModerationAppealsUpdated, appeal.UpdatedAt),
    };
    if (appeal.IsOverdue == true)
      lines.Add(UiCopy.Localize(UiMessageKey.NativeSwiftModerationAppealsOverdue));
    Add(lines, UiMessageKey.NativeSwiftModerationAppealsAiDrafted, appeal.AiDraftedAt, appeal.Model);
    Add(lines, UiMessageKey.NativeSwiftModerationAppealsDrafted, appeal.DraftedAt);
    Add(lines, UiMessageKey.NativeSwiftModerationAppealsEdited, appeal.EditedAt, appeal.EditedById);
    Add(lines, UiMessageKey.NativeSwiftModerationAppealsApproved, appeal.ApprovedAt, appeal.ApprovedById);
    Add(lines, UiMessageKey.NativeSwiftModerationAppealsSent, appeal.SentAt);
    Add(lines, UiMessageKey.NativeSwiftModerationAppealsResolved, appeal.ResolvedAt, appeal.ResolvedById);
    if (appeal.ResolutionAction is { } action)
    {
      lines.Add(UiCopy.Format(
          UiMessageKey.NativeSwiftModerationAppealsResolution,
          ("action", Action(action))));
    }
    return string.Join(Environment.NewLine, lines);
  }

  private static UiText Target(ModerationAppeal appeal) => appeal.TargetContext switch
  {
    ModerationAppealWarningContext =>
        UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsWarningAppeal),
    ModerationAppealCommunityBanContext =>
        UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsCommunityBanAppeal),
    ModerationAppealPostRemovalContext { Kind: ModerationAppealPostRemovalKind.Platform } =>
        UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsPlatformPostRemovalAppeal),
    ModerationAppealPostRemovalContext { Kind: ModerationAppealPostRemovalKind.Community } =>
        UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsCommunityPostRemovalAppeal),
    ModerationAppealSuspensionContext =>
        UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsSuspensionAppeal),
    _ => appeal.UserWarningId is { } warningId
          ? Localized(UiMessageKey.NativeSwiftModerationAppealsWarningTarget, warningId)
          : appeal.UserSuspensionId is { } suspensionId
              ? Localized(UiMessageKey.NativeSwiftModerationAppealsSuspensionTarget, suspensionId)
              : appeal.CommunityBanId is { } banId
                  ? Localized(UiMessageKey.NativeSwiftModerationAppealsCommunityBanTarget, banId)
                  : appeal.PostId is { } postId
                      ? Localized(UiMessageKey.NativeSwiftModerationAppealsPostTarget, postId)
                      : UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsUnknownTarget),
  };

  private static UiText Localized(UiMessageKey key, string id) =>
      UiText.Localized(key, ("id", UiText.ProtocolValue(id)));

  private static UiText Status(ModerationAppealStatus status) => UiText.Localized(status switch
  {
    ModerationAppealStatus.Pending => UiMessageKey.NativeSwiftModerationReportsPending,
    ModerationAppealStatus.Resolved => UiMessageKey.NativeSwiftModerationAppealsResolved,
    ModerationAppealStatus.Dismissed => UiMessageKey.NativeSwiftModerationReportsDismissed,
    _ => UiMessageKey.NativeSwiftModerationAppealsUnknown,
  });

  private static UiText Action(ModerationAppealAction action) => UiText.Localized(action switch
  {
    ModerationAppealAction.Accept => UiMessageKey.NativeSwiftModerationAppealsAccept,
    ModerationAppealAction.Reduce => UiMessageKey.NativeSwiftModerationAppealsReduce,
    ModerationAppealAction.Deny => UiMessageKey.NativeSwiftModerationAppealsDeny,
    _ => UiMessageKey.NativeSwiftModerationAppealsUnknown,
  });

  private static string Event(UiMessageKey key, DateTimeOffset timestamp) =>
      UiCopy.Format(
          key,
          ("date", UiCopy.FormatDateTime(timestamp)));

  private static void Add(
      List<string> lines,
      UiMessageKey key,
      DateTimeOffset? timestamp,
      string? actor = null)
  {
    if (timestamp is not { } value) return;
    lines.Add(UiCopy.Format(
        actor is null
            ? UiMessageKey.NativeSwiftModerationAppealsLifecycleEvent
            : UiMessageKey.NativeSwiftModerationAppealsLifecycleByActor,
        ("event", UiText.Localized(key)),
        ("date", UiCopy.FormatDateTime(value)),
        ("actor", UiText.UserContent(actor))));
  }
}

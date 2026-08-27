using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class MemberAppealsPage
{
  private static View AppealCard(ModerationAppeal appeal)
  {
    var content = new VerticalStackLayout
    {
      Children =
      {
        new Label
        {
          AutomationId = $"member-appeals-status-{StatusId(appeal.Status)}",
          Text = UiCopy.Resolve(AppealStatusText(appeal.Status)),
          FontAttributes = FontAttributes.Bold,
        },
        new Label
        {
          AutomationId = $"member-appeal-target-{appeal.Id}",
          Text = UiCopy.Resolve(MemberTargetText(appeal)),
        },
        new Label
        {
          AutomationId = $"member-appeal-created-{appeal.Id}",
          Text = UiCopy.Format(
              UiMessageKey.NativeSwiftModerationAppealsCreated,
              ("date", UiCopy.FormatDateTime(appeal.CreatedAt))),
        },
      },
    };
    if (appeal.PublicResponse is { } publicResponse)
    {
      content.Add(new Label
      {
        AutomationId = $"member-appeal-response-{appeal.Id}",
        Text = UiCopy.Format(
            UiMessageKey.NativeSwiftModerationAppealsLabelValue,
            ("label", UiText.Localized(
                UiMessageKey.NativeSwiftModerationAppealsPublicResponse)),
            ("value", UiText.UserContent(publicResponse))),
      });
    }
    AddLifecycle(content, appeal, UiMessageKey.NativeSwiftModerationAppealsApproved,
        appeal.ApprovedAt, "approved");
    AddLifecycle(content, appeal, UiMessageKey.NativeSwiftModerationAppealsSent,
        appeal.SentAt, "sent");
    AddLifecycle(content, appeal,
        appeal.Status == ModerationAppealStatus.Dismissed
            ? UiMessageKey.NativeSwiftModerationReportsDismissed
            : UiMessageKey.NativeSwiftModerationAppealsResolved,
        appeal.ResolvedAt, "resolved");
    if (appeal.ResolutionAction is { } action)
    {
      content.Add(new Label
      {
        AutomationId = $"member-appeal-resolution-{appeal.Id}",
        Text = UiCopy.Format(
            UiMessageKey.NativeSwiftModerationAppealsResolution,
            ("action", ResolutionActionText(action))),
      });
    }
    return new Border
    {
      AutomationId = $"member-appeal-{appeal.Id}",
      Padding = 12,
      Content = content,
    };
  }

  private static void AddLifecycle(
      VerticalStackLayout content,
      ModerationAppeal appeal,
      UiMessageKey key,
      DateTimeOffset? timestamp,
      string id)
  {
    if (timestamp is not { } value) return;
    content.Add(new Label
    {
      AutomationId = $"member-appeal-{id}-{appeal.Id}",
      Text = UiCopy.Format(
          UiMessageKey.NativeSwiftModerationAppealsLifecycleEvent,
          ("event", UiText.Localized(key)),
          ("date", UiCopy.FormatDateTime(value))),
    });
  }

  private static UiText AppealStatusText(ModerationAppealStatus status) =>
      UiText.Localized(status switch
      {
        ModerationAppealStatus.Pending => UiMessageKey.NativeSwiftModerationReportsPending,
        ModerationAppealStatus.Resolved => UiMessageKey.NativeSwiftModerationAppealsResolved,
        ModerationAppealStatus.Dismissed => UiMessageKey.NativeSwiftModerationReportsDismissed,
        _ => UiMessageKey.NativeSwiftModerationAppealsUnknown,
      });

  private static UiText ResolutionActionText(ModerationAppealAction action) =>
      UiText.Localized(action switch
      {
        ModerationAppealAction.Accept => UiMessageKey.NativeSwiftModerationAppealsAccept,
        ModerationAppealAction.Reduce => UiMessageKey.NativeSwiftModerationAppealsReduce,
        ModerationAppealAction.Deny => UiMessageKey.NativeSwiftModerationAppealsDeny,
        _ => UiMessageKey.NativeSwiftModerationAppealsUnknown,
      });

  private static UiText TargetText(MemberAppealTarget target)
  {
    var display = target.Summary ?? target.CommunitySlug;
    return display is not null
        ? UiText.UserContent(display)
        : UiText.ProtocolValue(target.TargetType.ToString());
  }

  private static UiText MemberTargetText(ModerationAppeal appeal) =>
      appeal.TargetContext switch
      {
        ModerationAppealWarningContext warning =>
          UserContext(warning.PublicMessage, warning.Community?.Name) ??
          TargetFallback(
              UiMessageKey.NativeSwiftModerationAppealsWarningTarget,
              warning.Id),
        ModerationAppealCommunityBanContext ban =>
          UiText.UserContent(ban.Community.Name),
        ModerationAppealPostRemovalContext removal =>
          UiText.UserContent(removal.Title),
        ModerationAppealSuspensionContext suspension =>
          UserContext(suspension.Reason) ??
          TargetFallback(
              UiMessageKey.NativeSwiftModerationAppealsSuspensionTarget,
              suspension.Id),
        _ => MemberTargetFallback(appeal),
      };

  private static UiText? UserContext(params string?[] values)
  {
    var value = values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
    return value is null ? null : UiText.UserContent(value);
  }

  private static UiText MemberTargetFallback(ModerationAppeal appeal)
  {
    if (appeal.UserWarningId is { } warningId)
      return TargetFallback(UiMessageKey.NativeSwiftModerationAppealsWarningTarget, warningId);
    if (appeal.CommunityBanId is { } banId)
      return TargetFallback(UiMessageKey.NativeSwiftModerationAppealsCommunityBanTarget, banId);
    if (appeal.PostId is { } postId)
      return TargetFallback(UiMessageKey.NativeSwiftModerationAppealsPostTarget, postId);
    if (appeal.UserSuspensionId is { } suspensionId)
      return TargetFallback(
          UiMessageKey.NativeSwiftModerationAppealsSuspensionTarget,
          suspensionId);
    return UiText.Localized(UiMessageKey.NativeSwiftModerationAppealsUnknownTarget);
  }

  private static UiText TargetFallback(UiMessageKey key, string id) =>
      UiText.Localized(key, ("id", UiText.ProtocolValue(id)));
}

using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>Copy for report, review, warning, appeal and ban notifications.</summary>
internal static class NotificationModerationText
{
  public static NotificationText ReportReviewed(NotificationRenderInput input) =>
      Fixed(UiMessageKey.NotificationsKindReportReviewedTitle, UiMessageKey.NotificationsKindReportReviewedBody);

  public static NotificationText CriticalModerationAlert(NotificationRenderInput input) =>
      NotificationText.TitleOnly(UiText.Localized(UiMessageKey.NotificationsKindCriticalModerationAlertTitle));

  public static NotificationText ReviewDisputeResolved(NotificationRenderInput input) =>
      PublicBody(
          UiMessageKey.NotificationsKindReviewDisputeResolvedTitle,
          input.PublicText(input.Notification.ReviewDisputeId));

  public static NotificationText ReviewActionedRemoved(NotificationRenderInput input) =>
      Fixed(
          UiMessageKey.NotificationsKindReviewActionedRemovedTitle,
          UiMessageKey.NotificationsKindReviewActionedRemovedBody);

  public static NotificationText ReviewActionedNote(NotificationRenderInput input) =>
      Fixed(
          UiMessageKey.NotificationsKindReviewActionedNoteTitle,
          UiMessageKey.NotificationsKindReviewActionedNoteBody);

  public static NotificationText UserWarning(NotificationRenderInput input) =>
      input.PublicText(input.Notification.UserWarningId) is { Length: > 0 } text
          ? PublicBody(UiMessageKey.NotificationsKindUserWarningTitle, text)
          : Fixed(UiMessageKey.NotificationsKindUserWarningTitle, UiMessageKey.NotificationsKindUserWarningBody);

  public static NotificationText ModerationAppealResolved(NotificationRenderInput input) =>
      PublicBody(
          UiMessageKey.NotificationsKindModerationAppealResolvedTitle,
          input.PublicText(input.Notification.ModerationAppealId));

  public static NotificationText CommunityBan(NotificationRenderInput input) =>
      Fixed(UiMessageKey.NotificationsKindCommunityBanTitle, UiMessageKey.NotificationsKindCommunityBanBody);

  private static NotificationText Fixed(UiMessageKey title, UiMessageKey body) =>
      NotificationText.Of(UiText.Localized(title), UiText.Localized(body));

  private static NotificationText PublicBody(UiMessageKey title, string publicText) =>
      new(UiText.Localized(title), publicText.Length == 0 ? [] : [UiText.UserContent(publicText)]);
}

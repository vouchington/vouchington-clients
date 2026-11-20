using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>Copy for the copyright notice delivery kinds.</summary>
internal static class NotificationCopyrightText
{
  public static NotificationText ClaimantReceipt(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightClaimantReceiptTitle,
          UiText.Localized(UiMessageKey.NotificationsKindCopyrightClaimantReceiptBody));

  public static NotificationText StatusUpdate(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightStatusUpdateTitle,
          UiText.Localized(UiMessageKey.NotificationsKindCopyrightStatusUpdateBody));

  public static NotificationText PosterRestrictionNotice(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightPosterRestrictionNoticeTitle,
          UiText.Localized(UiMessageKey.NotificationsKindCopyrightPosterRestrictionNoticeBody));

  public static NotificationText PosterReviewNotice(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightPosterReviewNoticeTitle,
          UiText.Localized(UiMessageKey.NotificationsKindCopyrightPosterReviewNoticeBody));

  public static NotificationText PosterRestorationNotice(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightPosterRestorationNoticeTitle,
          UiText.Localized(UiMessageKey.NotificationsKindCopyrightPosterRestorationNoticeBody));

  public static NotificationText OwnerInformationNotice(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightOwnerInformationNoticeTitle,
          UiText.Localized(UiMessageKey.NotificationsKindCopyrightOwnerInformationNoticeBody));

  public static NotificationText RedressDecisionNotice(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightRedressDecisionNoticeTitle,
          UiText.Localized(UiMessageKey.NotificationsKindCopyrightRedressDecisionNoticeBody));

  public static NotificationText ClaimantDecisionNotice(NotificationRenderInput input) =>
      WithSummary(
          input,
          UiMessageKey.NotificationsKindCopyrightClaimantDecisionNoticeTitle,
          DecisionFallback(input));

  private static UiText DecisionFallback(NotificationRenderInput input)
  {
    var jurisdiction = input.Notification.CopyrightNoticeId is { } id
        ? input.Hydration.CopyrightNotices?.GetValueOrDefault(id)?.Jurisdiction
        : null;
    return jurisdiction switch
    {
      "us_dmca" => UiText.Localized(
          UiMessageKey.NotificationsKindCopyrightClaimantDecisionNoticeBodyUs,
          ("newNoticeUrl", UiText.ProtocolValue(new Uri(input.WebBaseUrl, "/copyright/notices/new").AbsoluteUri)),
          ("designatedAgentUrl", UiText.ProtocolValue(new Uri(input.WebBaseUrl, "/copyright/designated-agent").AbsoluteUri))),
      "eu_dsa" => UiText.Localized(UiMessageKey.NotificationsKindCopyrightClaimantDecisionNoticeBodyEu),
      _ => UiText.Localized(UiMessageKey.NotificationsKindCopyrightClaimantDecisionNoticeBodyOther),
    };
  }

  /// <summary>The delivery summary, else the kind's fixed body, then the AI-assisted note when flagged.</summary>
  private static NotificationText WithSummary(
      NotificationRenderInput input,
      UiMessageKey title,
      UiText fallbackBody)
  {
    var summary = input.PublicText(input.Notification.CopyrightNoticeDeliveryWorkItemId);
    var body = summary.Length > 0 ? UiText.UserContent(summary) : fallbackBody;
    return new(
        UiText.Localized(title),
        input.BoolParam("ai_assisted")
            ? [body, UiText.Localized(UiMessageKey.NotificationsKindCopyrightAiAssisted)]
            : [body]);
  }
}

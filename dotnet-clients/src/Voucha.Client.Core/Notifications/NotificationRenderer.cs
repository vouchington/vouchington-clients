using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Notifications;

/// <summary>
/// Renders the localized title and body of a notification from its kind, params and hydrated facts.
/// An unknown or missing kind renders the generic title with no body.
/// </summary>
internal static class NotificationRenderer
{
  private static readonly IReadOnlyDictionary<string, Func<NotificationRenderInput, NotificationText>> Renderers =
      new Dictionary<string, Func<NotificationRenderInput, NotificationText>>(StringComparer.Ordinal)
      {
        ["post_new"] = NotificationPostText.PostNew,
        ["post_reply"] = NotificationPostText.PostReply,
        ["post_manual_send"] = NotificationPostText.PostManualSend,
        ["post_removed"] = NotificationPostText.PostRemoved,
        ["rss_item_new"] = NotificationPostText.RssItemNew,
        ["rss_item_manual_send"] = NotificationPostText.RssItemManualSend,
        ["follow"] = NotificationSocialText.Follow,
        ["follow_referral_signup"] = NotificationSocialText.FollowReferralSignup,
        ["referral_signup"] = NotificationSocialText.ReferralSignup,
        ["referral_click"] = NotificationSocialText.ReferralClick,
        ["direct_message"] = NotificationSocialText.DirectMessage,
        ["modmail"] = NotificationSocialText.Modmail,
        ["household_invitation"] = NotificationSocialText.HouseholdInvitation,
        ["report_reviewed"] = NotificationModerationText.ReportReviewed,
        ["critical_moderation_alert"] = NotificationModerationText.CriticalModerationAlert,
        ["review_dispute_resolved"] = NotificationModerationText.ReviewDisputeResolved,
        ["review_actioned_removed"] = NotificationModerationText.ReviewActionedRemoved,
        ["review_actioned_note"] = NotificationModerationText.ReviewActionedNote,
        ["user_warning"] = NotificationModerationText.UserWarning,
        ["moderation_appeal_resolved"] = NotificationModerationText.ModerationAppealResolved,
        ["community_ban"] = NotificationModerationText.CommunityBan,
        ["community_application_approved"] = NotificationCommunityText.ApplicationApproved,
        ["community_application_rejected"] = NotificationCommunityText.ApplicationRejected,
        ["community_role_changed"] = NotificationCommunityText.RoleChanged,
        ["community_ownership_received"] = NotificationCommunityText.OwnershipReceived,
        ["community_ownership_transferred_away"] = NotificationCommunityText.OwnershipTransferredAway,
        ["community_activity_digest"] = NotificationDigestText.Render,
        ["copyright_claimant_receipt"] = NotificationCopyrightText.ClaimantReceipt,
        ["copyright_status_update"] = NotificationCopyrightText.StatusUpdate,
        ["copyright_poster_restriction_notice"] = NotificationCopyrightText.PosterRestrictionNotice,
        ["copyright_poster_review_notice"] = NotificationCopyrightText.PosterReviewNotice,
        ["copyright_poster_restoration_notice"] = NotificationCopyrightText.PosterRestorationNotice,
        ["copyright_owner_information_notice"] = NotificationCopyrightText.OwnerInformationNotice,
        ["copyright_claimant_decision_notice"] = NotificationCopyrightText.ClaimantDecisionNotice,
        ["copyright_redress_decision_notice"] = NotificationCopyrightText.RedressDecisionNotice,
      };

  public static IReadOnlyCollection<string> Kinds => Renderers.Keys.ToArray();

  public static NotificationText Render(
      Notification notification,
      NotificationsResponse hydration,
      Uri webBaseUrl) =>
      notification.Kind is { } kind && Renderers.TryGetValue(kind, out var renderer)
          ? renderer(new NotificationRenderInput(notification, hydration, webBaseUrl))
          : NotificationText.Generic;
}

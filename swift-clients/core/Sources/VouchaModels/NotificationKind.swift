import Foundation

/// The kind of a notification row; copy is rendered client-side from the kind and its params.
public enum NotificationKind: String, Codable, Sendable, CaseIterable {
    case postNew = "post_new"
    case postReply = "post_reply"
    case postManualSend = "post_manual_send"
    case postRemoved = "post_removed"
    case rssItemNew = "rss_item_new"
    case rssItemManualSend = "rss_item_manual_send"
    case follow
    case followReferralSignup = "follow_referral_signup"
    case referralSignup = "referral_signup"
    case referralClick = "referral_click"
    case reportReviewed = "report_reviewed"
    case criticalModerationAlert = "critical_moderation_alert"
    case reviewDisputeResolved = "review_dispute_resolved"
    case reviewActionedRemoved = "review_actioned_removed"
    case reviewActionedNote = "review_actioned_note"
    case userWarning = "user_warning"
    case moderationAppealResolved = "moderation_appeal_resolved"
    case communityBan = "community_ban"
    case directMessage = "direct_message"
    case modmail
    case communityApplicationApproved = "community_application_approved"
    case communityApplicationRejected = "community_application_rejected"
    case communityRoleChanged = "community_role_changed"
    case communityOwnershipReceived = "community_ownership_received"
    case communityOwnershipTransferredAway = "community_ownership_transferred_away"
    case communityActivityDigest = "community_activity_digest"
    case householdInvitation = "household_invitation"
    case copyrightClaimantReceipt = "copyright_claimant_receipt"
    case copyrightStatusUpdate = "copyright_status_update"
    case copyrightPosterRestrictionNotice = "copyright_poster_restriction_notice"
    case copyrightPosterReviewNotice = "copyright_poster_review_notice"
    case copyrightPosterRestorationNotice = "copyright_poster_restoration_notice"
    case copyrightOwnerInformationNotice = "copyright_owner_information_notice"
    case copyrightClaimantDecisionNotice = "copyright_claimant_decision_notice"
    case copyrightRedressDecisionNotice = "copyright_redress_decision_notice"
    /// Fallback for kinds added server-side after this client was compiled.
    case unknown

    public init(from decoder: any Decoder) throws {
        let raw = try decoder.singleValueContainer().decode(String.self)
        self = NotificationKind(rawValue: raw) ?? .unknown
    }
}

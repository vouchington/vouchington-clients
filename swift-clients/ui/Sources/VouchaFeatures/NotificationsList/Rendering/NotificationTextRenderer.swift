import Foundation
import VouchaLocalization
import VouchaModels

/// Renders a notification row's copy from its kind, params and hydrated facts, mirroring the web
/// renderer (`web/components/notifications/notification-text*.ts`). One renderer per kind; an
/// unknown or unhandled kind falls back to the generic title.
public enum NotificationTextRenderer {
    typealias Renderer = (NotificationTextContext) -> NotificationContent

    static let renderers: [NotificationKind: Renderer] = [
        .postNew: postNew,
        .postReply: postReply,
        .postManualSend: postManualSend,
        .postRemoved: { _ in postRemoved() },
        .rssItemNew: rssItemNew,
        .rssItemManualSend: rssItemManualSend,
        .follow: follow,
        .followReferralSignup: followReferralSignup,
        .referralSignup: referralSignup,
        .referralClick: { _ in referralClick() },
        .directMessage: { _ in directMessage() },
        .modmail: { _ in modmail() },
        .householdInvitation: { _ in householdInvitation() },
        .reportReviewed: { _ in reportReviewed() },
        .criticalModerationAlert: { _ in criticalModerationAlert() },
        .reviewDisputeResolved: reviewDisputeResolved,
        .reviewActionedRemoved: { _ in reviewActionedRemoved() },
        .reviewActionedNote: { _ in reviewActionedNote() },
        .userWarning: userWarning,
        .moderationAppealResolved: moderationAppealResolved,
        .communityBan: { _ in communityBan() },
        .communityApplicationApproved: communityApplicationApproved,
        .communityApplicationRejected: communityApplicationRejected,
        .communityRoleChanged: communityRoleChanged,
        .communityOwnershipReceived: communityOwnershipReceived,
        .communityOwnershipTransferredAway: communityOwnershipTransferredAway,
        .communityActivityDigest: communityActivityDigest,
        .copyrightClaimantReceipt: copyrightClaimantReceipt,
        .copyrightStatusUpdate: copyrightStatusUpdate,
        .copyrightPosterRestrictionNotice: copyrightPosterRestrictionNotice,
        .copyrightPosterReviewNotice: copyrightPosterReviewNotice,
        .copyrightPosterRestorationNotice: copyrightPosterRestorationNotice,
        .copyrightOwnerInformationNotice: copyrightOwnerInformationNotice,
        .copyrightClaimantDecisionNotice: copyrightClaimantDecisionNotice,
        .copyrightRedressDecisionNotice: copyrightRedressDecisionNotice
    ]

    public static func render(
        _ notification: VouchaNotification,
        hydration: NotificationHydration,
        webOrigin: URL
    ) -> NotificationContent {
        guard let renderer = renderers[notification.kind] else {
            return NotificationContent(title: .message(.notificationsKindGenericTitle))
        }
        return renderer(NotificationTextContext(
            notification: notification,
            hydration: hydration,
            webOrigin: webOrigin
        ))
    }
}

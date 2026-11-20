import VouchaLocalization
import VouchaModels

extension NotificationTextRenderer {
    static func reportReviewed() -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindReportReviewedTitle),
            body: .message(.notificationsKindReportReviewedBody)
        )
    }

    static func criticalModerationAlert() -> NotificationContent {
        NotificationContent(title: .message(.notificationsKindCriticalModerationAlertTitle))
    }

    static func reviewDisputeResolved(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindReviewDisputeResolvedTitle),
            body: context.publicText(context.notification.reviewDisputeId)
        )
    }

    static func reviewActionedRemoved() -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindReviewActionedRemovedTitle),
            body: .message(.notificationsKindReviewActionedRemovedBody)
        )
    }

    static func reviewActionedNote() -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindReviewActionedNoteTitle),
            body: .message(.notificationsKindReviewActionedNoteBody)
        )
    }

    static func userWarning(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindUserWarningTitle),
            body: context.publicText(context.notification.userWarningId)
                ?? .message(.notificationsKindUserWarningBody)
        )
    }

    static func moderationAppealResolved(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindModerationAppealResolvedTitle),
            body: context.publicText(context.notification.moderationAppealId)
        )
    }

    static func communityBan() -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindCommunityBanTitle),
            body: .message(.notificationsKindCommunityBanBody)
        )
    }
}

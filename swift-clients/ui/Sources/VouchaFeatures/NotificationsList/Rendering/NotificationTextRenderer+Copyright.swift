import VouchaLocalization
import VouchaModels

extension NotificationTextRenderer {
    /// Fixed title; the body is the delivered summary or the kind's fallback, plus the AI note.
    private static func copyright(
        _ context: NotificationTextContext,
        title: UiMessageKey,
        fallbackBody: UiVerbatimText
    ) -> NotificationContent {
        let body = context.publicText(context.notification.copyrightNoticeDeliveryWorkItemId) ?? fallbackBody
        guard context.params.bool("ai_assisted") else {
            return NotificationContent(title: .message(title), body: body)
        }
        return NotificationContent(
            title: .message(title),
            body: .joined([body, .message(.notificationsKindCopyrightAiAssisted)], separator: "\n\n")
        )
    }

    static func copyrightClaimantReceipt(_ context: NotificationTextContext) -> NotificationContent {
        copyright(
            context,
            title: .notificationsKindCopyrightClaimantReceiptTitle,
            fallbackBody: .message(.notificationsKindCopyrightClaimantReceiptBody)
        )
    }

    static func copyrightStatusUpdate(_ context: NotificationTextContext) -> NotificationContent {
        copyright(
            context,
            title: .notificationsKindCopyrightStatusUpdateTitle,
            fallbackBody: .message(.notificationsKindCopyrightStatusUpdateBody)
        )
    }

    static func copyrightPosterRestrictionNotice(_ context: NotificationTextContext) -> NotificationContent {
        copyright(
            context,
            title: .notificationsKindCopyrightPosterRestrictionNoticeTitle,
            fallbackBody: .message(.notificationsKindCopyrightPosterRestrictionNoticeBody)
        )
    }

    static func copyrightPosterReviewNotice(_ context: NotificationTextContext) -> NotificationContent {
        copyright(
            context,
            title: .notificationsKindCopyrightPosterReviewNoticeTitle,
            fallbackBody: .message(.notificationsKindCopyrightPosterReviewNoticeBody)
        )
    }

    static func copyrightPosterRestorationNotice(_ context: NotificationTextContext) -> NotificationContent {
        copyright(
            context,
            title: .notificationsKindCopyrightPosterRestorationNoticeTitle,
            fallbackBody: .message(.notificationsKindCopyrightPosterRestorationNoticeBody)
        )
    }

    static func copyrightOwnerInformationNotice(_ context: NotificationTextContext) -> NotificationContent {
        copyright(
            context,
            title: .notificationsKindCopyrightOwnerInformationNoticeTitle,
            fallbackBody: .message(.notificationsKindCopyrightOwnerInformationNoticeBody)
        )
    }

    static func copyrightRedressDecisionNotice(_ context: NotificationTextContext) -> NotificationContent {
        copyright(
            context,
            title: .notificationsKindCopyrightRedressDecisionNoticeTitle,
            fallbackBody: .message(.notificationsKindCopyrightRedressDecisionNoticeBody)
        )
    }

    /// The fallback depends on the notice's jurisdiction: US DMCA, EU DSA, or other.
    static func copyrightClaimantDecisionNotice(_ context: NotificationTextContext) -> NotificationContent {
        let noticeId = context.notification.copyrightNoticeId
        let jurisdiction = noticeId.flatMap { context.hydration.copyrightNotices[$0]?.jurisdiction }
        let fallback: UiVerbatimText = switch jurisdiction {
        case "us_dmca":
            .message(.notificationsKindCopyrightClaimantDecisionNoticeBodyUs, textParameters: [
                "newNoticeUrl": context.webURL("/copyright/notices/new"),
                "designatedAgentUrl": context.webURL("/copyright/designated-agent")
            ])
        case "eu_dsa":
            .message(.notificationsKindCopyrightClaimantDecisionNoticeBodyEu)
        default:
            .message(.notificationsKindCopyrightClaimantDecisionNoticeBodyOther)
        }
        return copyright(
            context,
            title: .notificationsKindCopyrightClaimantDecisionNoticeTitle,
            fallbackBody: fallback
        )
    }
}

import VouchaLocalization
import VouchaModels

extension NotificationTextRenderer {
    static func follow(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(title: context.byUser(
            .notificationsKindFollowByUser,
            .notificationsKindFollowAnonymous,
            userId: context.notification.actorUserId
        ))
    }

    static func followReferralSignup(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(title: context.byUser(
            .notificationsKindFollowReferralSignupByUser,
            .notificationsKindFollowReferralSignupAnonymous,
            userId: context.notification.actorUserId
        ))
    }

    static func referralSignup(_ context: NotificationTextContext) -> NotificationContent {
        let count = context.params.number("referral_count")
        return NotificationContent(
            title: context.byUser(
                .notificationsKindReferralSignupByUser,
                .notificationsKindReferralSignupAnonymous,
                userId: context.notification.actorUserId
            ),
            body: .message(
                count == 1 ? .notificationsKindReferralSignupBodyOne : .notificationsKindReferralSignupBodyOther,
                numberParameters: ["count": count]
            )
        )
    }

    static func referralClick() -> NotificationContent {
        NotificationContent(title: .message(.notificationsKindReferralClickTitle))
    }

    static func directMessage() -> NotificationContent {
        NotificationContent(title: .message(.notificationsKindDirectMessageTitle))
    }

    static func modmail() -> NotificationContent {
        NotificationContent(title: .message(.notificationsKindModmailTitle))
    }

    static func householdInvitation() -> NotificationContent {
        NotificationContent(
            title: .message(.notificationsKindHouseholdInvitationTitle),
            body: .message(.notificationsKindHouseholdInvitationBody)
        )
    }
}

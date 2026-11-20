import VouchaLocalization
import VouchaModels

extension NotificationTextRenderer {
    static func communityApplicationApproved(_ context: NotificationTextContext) -> NotificationContent {
        let community = ["community": context.communityName()]
        return NotificationContent(
            title: .message(.notificationsKindCommunityApplicationApprovedTitle, textParameters: community),
            body: .message(.notificationsKindCommunityApplicationApprovedBody, textParameters: community)
        )
    }

    static func communityApplicationRejected(_ context: NotificationTextContext) -> NotificationContent {
        let community = ["community": context.communityName()]
        let body: UiVerbatimText = if let reason = context.publicText(context.notification.communityApplicationId) {
            .message(.notificationsKindCommunityApplicationRejectedBodyWithReason, textParameters: ["reason": reason])
        } else {
            .message(.notificationsKindCommunityApplicationRejectedBody, textParameters: community)
        }
        return NotificationContent(
            title: .message(.notificationsKindCommunityApplicationRejectedTitle, textParameters: community),
            body: body
        )
    }

    static func communityRoleChanged(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(
            title: .message(
                .notificationsKindCommunityRoleChangedTitle,
                textParameters: ["community": context.communityName()]
            ),
            body: .message(.notificationsKindCommunityRoleChangedBody, textParameters: [
                "from": roleName(context.params.string("from_role")),
                "to": roleName(context.params.string("to_role"))
            ])
        )
    }

    static func communityOwnershipReceived(_ context: NotificationTextContext) -> NotificationContent {
        let community = ["community": context.communityName()]
        return NotificationContent(
            title: .message(.notificationsKindCommunityOwnershipReceivedTitle, textParameters: community),
            body: .message(.notificationsKindCommunityOwnershipReceivedBody, textParameters: community)
        )
    }

    static func communityOwnershipTransferredAway(_ context: NotificationTextContext) -> NotificationContent {
        let community = ["community": context.communityName()]
        return NotificationContent(
            title: .message(.notificationsKindCommunityOwnershipTransferredAwayTitle, textParameters: community),
            body: .message(.notificationsKindCommunityOwnershipTransferredAwayBody, textParameters: community)
        )
    }

    /// Owner and moderator are named; any other role is member.
    private static func roleName(_ role: String?) -> UiVerbatimText {
        switch role {
        case "owner": .message(.notificationsKindRoleOwner)
        case "moderator": .message(.notificationsKindRoleModerator)
        default: .message(.notificationsKindRoleMember)
        }
    }
}

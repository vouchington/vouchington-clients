import VouchaLocalization
import VouchaModels

private struct PostTitleKeys {
    let byUser: UiMessageKey
    let anonymous: UiMessageKey
}

private struct PostTitleSet {
    let review: PostTitleKeys
    let discussion: PostTitleKeys
    let dataPoint: PostTitleKeys
    let link: PostTitleKeys
    let other: PostTitleKeys

    /// Post types are review, discussion, data_point, link and other; unknown types are other.
    func keys(for rawType: String?) -> PostTitleKeys {
        switch rawType.flatMap(PostType.init(rawValue:)) {
        case .review: review
        case .discussion: discussion
        case .dataPoint: dataPoint
        case .link: link
        default: other
        }
    }

    static let new = PostTitleSet(
        review: PostTitleKeys(
            byUser: .notificationsKindPostNewReviewByUser,
            anonymous: .notificationsKindPostNewReviewAnonymous
        ),
        discussion: PostTitleKeys(
            byUser: .notificationsKindPostNewDiscussionByUser,
            anonymous: .notificationsKindPostNewDiscussionAnonymous
        ),
        dataPoint: PostTitleKeys(
            byUser: .notificationsKindPostNewDataPointByUser,
            anonymous: .notificationsKindPostNewDataPointAnonymous
        ),
        link: PostTitleKeys(
            byUser: .notificationsKindPostNewLinkByUser,
            anonymous: .notificationsKindPostNewLinkAnonymous
        ),
        other: PostTitleKeys(
            byUser: .notificationsKindPostNewOtherByUser,
            anonymous: .notificationsKindPostNewOtherAnonymous
        )
    )

    static let manualSend = PostTitleSet(
        review: PostTitleKeys(
            byUser: .notificationsKindPostManualSendReviewByUser,
            anonymous: .notificationsKindPostManualSendReviewAnonymous
        ),
        discussion: PostTitleKeys(
            byUser: .notificationsKindPostManualSendDiscussionByUser,
            anonymous: .notificationsKindPostManualSendDiscussionAnonymous
        ),
        dataPoint: PostTitleKeys(
            byUser: .notificationsKindPostManualSendDataPointByUser,
            anonymous: .notificationsKindPostManualSendDataPointAnonymous
        ),
        link: PostTitleKeys(
            byUser: .notificationsKindPostManualSendLinkByUser,
            anonymous: .notificationsKindPostManualSendLinkAnonymous
        ),
        other: PostTitleKeys(
            byUser: .notificationsKindPostManualSendOtherByUser,
            anonymous: .notificationsKindPostManualSendOtherAnonymous
        )
    )
}

extension NotificationTextRenderer {
    static func postNew(_ context: NotificationTextContext) -> NotificationContent {
        let post = context.post
        let keys = PostTitleSet.new.keys(for: post?.postType)
        return NotificationContent(
            title: context.byUser(keys.byUser, keys.anonymous, userId: post?.actorUserId),
            body: NotificationTextContext.snippet(post?.snippet)
        )
    }

    static func postReply(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(
            title: context.byUser(
                .notificationsKindPostReplyByUser,
                .notificationsKindPostReplyAnonymous,
                userId: context.post?.actorUserId
            ),
            body: NotificationTextContext.snippet(context.post?.snippet)
        )
    }

    static func postManualSend(_ context: NotificationTextContext) -> NotificationContent {
        let keys = PostTitleSet.manualSend.keys(for: context.post?.postType)
        return NotificationContent(
            title: context.byUser(keys.byUser, keys.anonymous, userId: context.notification.sentById),
            body: NotificationTextContext.snippet(context.post?.snippet)
        )
    }

    static func postRemoved() -> NotificationContent {
        NotificationContent(title: .message(.notificationsKindPostRemovedTitle))
    }

    static func rssItemNew(_ context: NotificationTextContext) -> NotificationContent {
        let item = context.feedItem
        let feedName: UiVerbatimText = item.map { .userContent($0.feedName) }
            ?? .message(.notificationsKindRssItemNewFallbackFeed)
        return NotificationContent(
            title: .message(.notificationsKindRssItemNewTitle, textParameters: ["feedName": feedName]),
            body: NotificationTextContext.snippet(item?.snippet)
        )
    }

    static func rssItemManualSend(_ context: NotificationTextContext) -> NotificationContent {
        NotificationContent(
            title: context.byUser(
                .notificationsKindRssItemManualSendByUser,
                .notificationsKindRssItemManualSendAnonymous,
                userId: context.notification.sentById
            ),
            body: NotificationTextContext.snippet(context.feedItem?.snippet)
        )
    }
}

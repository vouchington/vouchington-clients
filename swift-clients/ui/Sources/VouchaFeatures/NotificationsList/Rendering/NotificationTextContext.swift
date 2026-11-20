import Foundation
import VouchaLocalization
import VouchaModels

/// The sentence a notification row shows. A nil body means the row has only a title.
public struct NotificationContent: Equatable, Sendable {
    public let title: UiVerbatimText
    public let body: UiVerbatimText?

    public init(title: UiVerbatimText, body: UiVerbatimText? = nil) {
        self.title = title
        self.body = body
    }
}

/// One notification plus the hydrated facts and web origin its renderer reads.
struct NotificationTextContext {
    let notification: VouchaNotification
    let hydration: NotificationHydration
    let webOrigin: URL

    var params: NotificationParams {
        notification.params
    }

    /// The username of a hydrated user, or nil when the user is gone, hidden or anonymous.
    func username(_ userId: String?) -> String? {
        guard let userId, let name = hydration.actors[userId]?.username, !name.isEmpty else { return nil }
        return name
    }

    /// `byUser` copy with `@{username}`, or the `anonymous` copy when the user is not hydrated.
    func byUser(_ byUser: UiMessageKey, _ anonymous: UiMessageKey, userId: String?) -> UiVerbatimText {
        guard let name = username(userId) else { return .message(anonymous) }
        return .message(byUser, textParameters: ["username": .userContent(name)])
    }

    /// The community's display name, or the catalog fallback when it is gone or not visible.
    func communityName(_ communityId: String? = nil) -> UiVerbatimText {
        let id = communityId ?? notification.communityId
        guard let id, let community = hydration.communities[id] else {
            return .message(.notificationsKindFallbackCommunity)
        }
        return .userContent(community.name)
    }

    /// Moderator-authored text for the row that owns it; nil when it is unsent or gone.
    func publicText(_ ownerId: String?) -> UiVerbatimText? {
        guard let ownerId, let text = hydration.publicTexts[ownerId], !text.isEmpty else { return nil }
        return .userContent(text)
    }

    var post: NotificationPost? {
        notification.postId.flatMap { hydration.posts[$0] }
    }

    var feedItem: NotificationRssFeedItem? {
        notification.rssFeedItemId.flatMap { hydration.rssFeedItems[$0] }
    }

    /// An absolute web URL for a site path, built from the configured web origin.
    func webURL(_ path: String) -> UiVerbatimText {
        .protocolValue(webOrigin.appending(path: path).absoluteString)
    }

    static func snippet(_ text: String?) -> UiVerbatimText? {
        guard let text, !text.isEmpty else { return nil }
        return .userContent(text)
    }
}

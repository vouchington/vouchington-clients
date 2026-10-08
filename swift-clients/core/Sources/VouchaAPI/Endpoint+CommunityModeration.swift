import Foundation
import VouchaModels

private struct EmptyBody: Encodable {}

private struct CommunityModmailThreadUpdateBody: Encodable {
    let assignedModeratorUserId: String?
    let resolved: Bool?
}

public extension Endpoint {
    static func communityModlog(idOrSlug: String, after: String? = nil, actionType: String? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let actionType {
            items.append(.init(name: "action_type", value: actionType))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/modlog", queryItems: items)
    }

    static func communityModmail(idOrSlug: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/modmail", queryItems: items)
    }

    static func openCommunityModmailThread(idOrSlug: String, subjectUserId: String? = nil) -> Endpoint {
        if let subjectUserId {
            return Endpoint(
                .POST,
                path: "/api/v1/communities/\(pathSegment(idOrSlug))/modmail",
                body: ["subject_user_id": subjectUserId]
            )
        }
        return Endpoint(.POST, path: "/api/v1/communities/\(pathSegment(idOrSlug))/modmail", body: EmptyBody())
    }

    static func openCommunityModmailThreadForReport(idOrSlug: String, reportId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/reports/\(pathSegment(reportId))/modmail",
            body: EmptyBody()
        )
    }

    static func communityModmailMessages(
        idOrSlug: String,
        conversationId: String,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/modmail/\(pathSegment(conversationId))/messages",
            queryItems: items
        )
    }

    static func sendCommunityModmailMessage(idOrSlug: String, conversationId: String, text: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/modmail/\(pathSegment(conversationId))/messages",
            body: ["text": text]
        )
    }

    static func updateCommunityModmailThread(
        idOrSlug: String,
        conversationId: String,
        assignedModeratorUserId: String? = nil,
        resolved: Bool? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/modmail/\(pathSegment(conversationId))",
            body: CommunityModmailThreadUpdateBody(assignedModeratorUserId: assignedModeratorUserId, resolved: resolved)
        )
    }

    static func communitySavedReplies(idOrSlug: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/saved-replies",
            queryItems: [
                URLQueryItem(name: "after", value: after),
                URLQueryItem(name: "limit", value: limit.map(String.init))
            ].compactMap { $0.value == nil ? nil : $0 }
        )
    }

    static func createCommunitySavedReply(idOrSlug: String, title: String, body: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/saved-replies",
            body: ["title": title, "body": body]
        )
    }

    static func deleteCommunitySavedReply(idOrSlug: String, replyId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/saved-replies/\(pathSegment(replyId))")
    }

    static func communityModerationQueue(
        idOrSlug: String, after: String? = nil, limit: Int? = nil,
        source: CommunityModerationQueueSource? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let source {
            items.append(.init(name: "source", value: source.rawValue))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderation-queue", queryItems: items)
    }

    static func claimCommunityModerationReport(idOrSlug: String, reportId: String) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/reports/\(pathSegment(reportId))/claim",
            body: EmptyBody()
        )
    }

    static func releaseCommunityModerationReport(idOrSlug: String, reportId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/reports/\(pathSegment(reportId))/claim")
    }

    static func claimCommunityPendingPost(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))/claim",
            body: EmptyBody()
        )
    }

    static func releaseCommunityPendingPost(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))/claim")
    }

    static func communityModerationAnalytics(idOrSlug: String, range: String? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let range {
            items.append(.init(name: "range", value: range))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderation-analytics",
            queryItems: items
        )
    }

    static func communityModerationTransparency(
        idOrSlug: String,
        range: String? = nil,
        after: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let range {
            items.append(.init(name: "range", value: range))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderation-transparency",
            queryItems: items
        )
    }
}

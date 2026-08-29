// swiftlint:disable file_length
import Foundation
import VouchaModels

struct CreateReferralLinkBody: Encodable {
    let referralProgramId: String
    let url: String
    let label: String?
}

public extension Endpoint {
    static var featureFlags: Endpoint {
        Endpoint(.GET, path: "/api/v1/feature-flags")
    }

    static var captchaConfig: Endpoint {
        Endpoint(.GET, path: "/api/v1/captcha-config")
    }

    static func user(idOrSlug: String, includeBio: Bool? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let includeBio {
            items.append(.init(name: "include_bio", value: includeBio ? "1" : "0"))
        }
        return Endpoint(.GET, path: "/api/v1/users/\(pathSegment(idOrSlug))", queryItems: items)
    }

    static func webSearch(query: String, limit: Int? = nil) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "query", value: query)]
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        return Endpoint(.GET, path: "/api/v1/web-search", queryItems: items)
    }

    static func fediverseSearch(
        query: String,
        providers: [String]? = nil,
        type: String? = nil,
        limit: Int? = nil,
        after: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "q", value: query)]
        if let providers, !providers.isEmpty {
            items.append(.init(name: "providers", value: providers.joined(separator: ",")))
        }
        if let type {
            items.append(.init(name: "type", value: type))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/fediverse/search", queryItems: items)
    }

    static func trendingCommunities(after: String? = nil, limit: Int = 10) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/trending-communities", queryItems: items)
    }

    static func trendingReferralPrograms(after: String? = nil, limit: Int = 10) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/trending-referral-programs", queryItems: items)
    }

    static func referralLinksFeed(feedType: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/feeds/referral_links/\(feedType)", queryItems: items)
    }

    static func recommendedTopics(
        after: String? = nil,
        limit: Int = 25,
        topicTypes: [String]? = nil,
        sort: RecommendedTopicsSort? = nil,
        spendingCategory: Bool? = nil,
        rssFeed: Bool? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let topicTypes, !topicTypes.isEmpty {
            items.append(.init(name: "topic_types", value: topicTypes.joined(separator: ",")))
        }
        if let sort {
            items.append(.init(name: "sort", value: sort.rawValue))
        }
        if let spendingCategory {
            items.append(.init(name: "spending_category", value: spendingCategory ? "true" : "false"))
        }
        if let rssFeed {
            items.append(.init(name: "rss_feed", value: rssFeed ? "true" : "false"))
        }
        return Endpoint(.GET, path: "/api/v1/recommended-topics", queryItems: items)
    }

    static func topicRecommendations(after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/topic-recommendations", queryItems: items)
    }

    static func topicRecommendation(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/topic-recommendations/\(pathSegment(id))")
    }

    static func topHashtags(
        query: String? = nil,
        mapping: TopHashtagMapping = .all,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items = [
            URLQueryItem(name: "limit", value: "\(limit)"),
            URLQueryItem(name: "mapping", value: mapping.rawValue)
        ]
        if let query, !query.isEmpty {
            items.append(.init(name: "q", value: query))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/topic-recommendations/top-hashtags", queryItems: items)
    }

    static func updateTopicRecommendation(id: String, body: some Encodable & Sendable) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/topic-recommendations/\(pathSegment(id))", body: body)
    }

    static var myCommunities: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/communities")
    }

    static func myReferralClicks(after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/referral-clicks", queryItems: items)
    }

    static func referralLinks(after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/referral-links", queryItems: items)
    }

    static func createReferralLink(referralProgramId: String, url: String, label: String?) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/referral-links",
            body: CreateReferralLinkBody(referralProgramId: referralProgramId, url: url, label: label)
        )
    }

    static func updateReferralLink(id: String, label: String?) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/referral-links/\(pathSegment(id))", body: ["label": label])
    }

    static func deleteReferralLink(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/referral-links/\(pathSegment(id))")
    }

    static func activateReferralLink(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/referral-links/\(pathSegment(id))/activations", body: [String: String]())
    }

    static func deactivateReferralLink(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/referral-links/\(pathSegment(id))/activations")
    }

    static func prioritizedReferralLinks(referralProgramId: String, all: Bool = false) -> Endpoint {
        let items = all ? [URLQueryItem(name: "all", value: "true")] : []
        return Endpoint(
            .GET,
            path: "/api/v1/topics/\(pathSegment(referralProgramId))/prioritized-referral-links",
            queryItems: items
        )
    }

    static func bookmarks(entityType: String, entityId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/bookmarks/\(entityType)/\(pathSegment(entityId))")
    }

    static func bookmark(entityType: String, entityId: String, predicate: String) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/bookmarks/\(entityType)/\(pathSegment(entityId))/\(predicate)")
    }

    static func unbookmark(entityType: String, entityId: String, predicate: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/bookmarks/\(entityType)/\(pathSegment(entityId))/\(predicate)")
    }

    static func myConversations(after: String? = nil, limit: Int = 50) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/conversations", queryItems: items)
    }

    static func myConversationMessages(
        conversationId: String,
        after: String? = nil,
        limit: Int = 50
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/my/conversations/\(pathSegment(conversationId))/messages",
            queryItems: items
        )
    }

    static func myConversationParticipants(
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
            path: "/api/v1/my/messages/\(pathSegment(conversationId))/participants",
            queryItems: items
        )
    }

    static func myMessages(after: String? = nil, limit: Int = 50) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/messages", queryItems: items)
    }

    static func myMessageConversationMessages(
        conversationId: String,
        after: String? = nil,
        limit: Int = 50
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/my/messages/\(pathSegment(conversationId))/messages",
            queryItems: items
        )
    }

    static func disputes(
        status: ModerationDisputeStatus? = nil,
        limit: Int = 25,
        after: String? = nil,
        mine: Bool = false
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let status {
            items.append(.init(name: "status", value: status.rawValue))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if mine {
            items.append(.init(name: "mine", value: "true"))
        }
        return Endpoint(.GET, path: "/api/v1/disputes", queryItems: items)
    }

}

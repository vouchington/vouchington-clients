import Foundation
import VouchaAPI

extension NativeRouteSurfaceViewModel {
    func bookmarkCollection(for userId: String) -> NativeBookmarkCollection? {
        guard let path = routeMatch?.path else { return nil }
        let parts = path.split(separator: "/").map(String.init)
        guard parts.count == 2 || parts.count == 3, parts[0] == "my" else { return nil }
        let segment = parts[1]
        let listType = parts.count == 2 ? "following" : parts[2]
        guard parts.count == 3 || ["news-sources", "podcasts", "channels"].contains(segment) else { return nil }

        if let endpoint = dismissedFriendRecommendationsEndpoint(
            userId: userId,
            segment: segment,
            listType: listType
        ) {
            return .init(
                endpoint: endpoint,
                kind: .users,
                icon: "person",
                entityType: "user",
                inverseAction: inverseAction(entityType: "user", listType: listType)
            )
        }

        let collection = bookmarkCollectionEndpoint(userId: userId, segment: segment, listType: listType)
        guard let collection else { return nil }
        return .init(
            endpoint: collection.endpoint,
            kind: collection.kind,
            icon: bookmarkCollectionIcon(segment: segment),
            entityType: bookmarkEntityType(segment: segment),
            inverseAction: inverseAction(
                entityType: bookmarkMutationEntityType(segment: segment),
                listType: listType
            )
        )
    }

    private func bookmarkCollectionEndpoint(
        userId: String,
        segment: String,
        listType: String
    ) -> (endpoint: Endpoint, kind: NativeBookmarkCollectionKind)? {
        switch segment {
        case "posts":
            (.userPosts(userId: userId, listType: listType), .posts)
        case "topics":
            (bookmarkEndpoint(userId: userId, entity: "topics", listType: listType, segment: segment), .generic)
        case "users":
            (bookmarkEndpoint(userId: userId, entity: "users", listType: listType, segment: segment), .generic)
        case "rss-feed-items", "news-items", "podcast-episodes", "videos":
            (
                bookmarkEndpoint(userId: userId, entity: "rss-feed-items", listType: listType, segment: segment),
                .rssFeedItems
            )
        case "urls":
            (bookmarkEndpoint(userId: userId, entity: "urls", listType: listType, segment: segment), .generic)
        case "domains":
            (bookmarkEndpoint(userId: userId, entity: "domains", listType: listType, segment: segment), .generic)
        case "communities":
            (bookmarkEndpoint(userId: userId, entity: "communities", listType: listType, segment: segment), .generic)
        case "news-sources", "podcasts", "channels":
            (bookmarkEndpoint(userId: userId, entity: "rss-feeds", listType: listType, segment: segment), .rssFeeds)
        default:
            nil
        }
    }

    private func bookmarkEndpoint(userId: String, entity: String, listType: String, segment: String) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/users/\(userId)/\(entity)/\(listType)",
            queryItems: bookmarkCollectionQueryItems(segment: segment)
        )
    }

    private func bookmarkCollectionQueryItems(segment: String) -> [URLQueryItem] {
        switch segment {
        case "rss-feed-items", "news-items":
            [.init(name: "media_type", value: "article")]
        case "podcast-episodes":
            [.init(name: "media_type", value: "audio")]
        case "videos":
            [.init(name: "media_type", value: "video")]
        case "news-sources":
            [.init(name: "feed_type", value: "article")]
        case "podcasts":
            [.init(name: "feed_type", value: "podcast")]
        case "channels":
            [.init(name: "feed_type", value: "video")]
        default:
            []
        }
    }

    private func bookmarkCollectionIcon(segment: String) -> String {
        switch segment {
        case "topics": "tag"
        case "users", "friend-recommendations": "person"
        case "urls": "link"
        case "domains": "globe"
        case "communities": "person.3"
        case "news-items", "podcast-episodes", "videos", "rss-feed-items": "newspaper"
        case "news-sources", "podcasts", "channels": "list.bullet.rectangle"
        default: "doc.text"
        }
    }

    private func bookmarkEntityType(segment: String) -> String {
        switch segment {
        case "posts": "post"
        case "topics": "topic"
        case "users", "friend-recommendations": "user"
        case "rss-feed-items", "news-items", "podcast-episodes", "videos": "rss_feed_item"
        case "news-sources", "podcasts", "channels": "rss_feed"
        case "urls": "url"
        case "domains": "hostname"
        case "communities": "community"
        default: segment
        }
    }

    private func bookmarkMutationEntityType(segment: String) -> String {
        segment == "domains" ? "url_hostname" : bookmarkEntityType(segment: segment)
    }

    private func dismissedFriendRecommendationsEndpoint(
        userId: String,
        segment: String,
        listType: String
    ) -> Endpoint? {
        guard segment == "friend-recommendations", listType == "dismissed" else { return nil }
        return Endpoint(.GET, path: "/api/v1/users/\(userId)/users/dismissed-recommendations")
    }
}

struct NativeBookmarkCollection {
    let endpoint: Endpoint
    let kind: NativeBookmarkCollectionKind
    let icon: String
    let entityType: String
    let inverseAction: NativeBookmarkRow.InverseAction?
}

enum NativeBookmarkCollectionKind: Equatable {
    case generic
    case posts
    case rssFeedItems
    case rssFeeds
    case users
}

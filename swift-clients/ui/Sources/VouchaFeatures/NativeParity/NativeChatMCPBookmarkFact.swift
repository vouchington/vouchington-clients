import Foundation
import VouchaModels

enum NativeChatBookmarkPredicate: String, Equatable {
    case save, follow, mute, block
}

enum NativeChatBookmarkEntityType: String, Equatable {
    case post, topic, user, community, url
    case rssFeed = "rss_feed"
    case rssFeedItem = "rss_feed_item"
    case urlHostname = "url_hostname"
}

/// Predicate and entity types only. Entity identifiers and bookmark rows never reach the fact.
struct NativeChatBookmarkFact: Equatable {
    enum Action: Equatable { case set, removed }

    let action: Action
    let predicate: NativeChatBookmarkPredicate
    let entityTypes: [NativeChatBookmarkEntityType]
}

extension NativeChatMCPFact {
    static func makeBookmarkSet(arguments: DecodedJSONValue?, content: DecodedJSONValue) -> Self? {
        guard case let .object(object)? = arguments,
              case let .string(token)? = object["predicate"],
              let predicate = NativeChatBookmarkPredicate(rawValue: token),
              let envelope = decodeContent(BookmarkSetEnvelope.self, content), envelope.success else { return nil }
        var types: [NativeChatBookmarkEntityType] = []
        for row in envelope.results ?? [] where row.status == "set" {
            guard let type = NativeChatBookmarkEntityType(rawValue: row.entityType) else { return nil }
            if !types.contains(type) { types.append(type) }
        }
        guard !types.isEmpty else { return nil }
        return .bookmark(.init(action: .set, predicate: predicate, entityTypes: types))
    }

    static func makeBookmarkRemove(arguments: DecodedJSONValue?, content: DecodedJSONValue) -> Self? {
        guard case let .object(object)? = arguments,
              case let .string(predicateToken)? = object["predicate"],
              case let .string(typeToken)? = object["entity_type"],
              let predicate = NativeChatBookmarkPredicate(rawValue: predicateToken),
              let type = NativeChatBookmarkEntityType(rawValue: typeToken),
              let envelope = decodeContent(BookmarkSetEnvelope.self, content), envelope.success,
              envelope.results == nil else { return nil }
        return .bookmark(.init(action: .removed, predicate: predicate, entityTypes: [type]))
    }
}

private struct BookmarkSetEnvelope: Decodable {
    let success: Bool
    let results: [BookmarkRow]?
}

private struct BookmarkRow: Decodable {
    let entityType: String
    let status: String
}

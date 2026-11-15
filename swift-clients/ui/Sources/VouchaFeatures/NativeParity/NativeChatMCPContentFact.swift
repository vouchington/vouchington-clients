import Foundation
import VouchaAPI
import VouchaModels

struct NativeChatPostSummaryFact: Equatable, Identifiable {
    let id: String
    let title: String
    let references: HackerNewsReferences?
}

struct NativeChatRssItemSummaryFact: Equatable, Identifiable {
    let id: String
    let title: String
    let feedTitle: String
    let references: HackerNewsReferences?
}

/// Posts, RSS items and stories expose only public titles and stored Hacker News counts.
/// Authorship, markdown, URLs and identifiers beyond the row key never reach a fact.
extension NativeChatMCPFact {
    static func makePostSearch(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(PostSearchEnvelope.self, value), envelope.success else { return nil }
        return posts(envelope.results)
    }

    static func makePostDetails(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(PostDetailsEnvelope.self, value), envelope.success else { return nil }
        return posts([envelope.post])
    }

    static func makeRssItemList(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(RssListEnvelope.self, value), envelope.success else { return nil }
        return rssItems(envelope.results)
    }

    static func makeRssItemDetails(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(RssDetailsEnvelope.self, value), envelope.success else { return nil }
        return rssItems([envelope.rssFeedItem])
    }

    static func makeStoryItems(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(StoryEnvelope.self, value), envelope.success else { return nil }
        return rssItems(envelope.items)
    }

    private static func posts(_ rows: [PostRow]) -> Self? {
        guard rows.count <= 100, unique(rows.map(\.id)),
              rows.allSatisfy({ displayable($0.title) && $0.hackerNewsReferences.isValid }) else { return nil }
        return .postSummaries(rows.map {
            NativeChatPostSummaryFact(id: $0.id, title: $0.title, references: $0.hackerNewsReferences)
        })
    }

    private static func rssItems(_ rows: [RssRow]) -> Self? {
        guard rows.count <= 100, unique(rows.map(\.id)),
              rows.allSatisfy({
                  displayable($0.title) && displayable($0.rssFeedTitle) && $0.hackerNewsReferences.isValid
              }) else { return nil }
        return .rssItemSummaries(rows.map {
            NativeChatRssItemSummaryFact(
                id: $0.id, title: $0.title, feedTitle: $0.rssFeedTitle, references: $0.hackerNewsReferences
            )
        })
    }

    private static func unique(_ ids: [String]) -> Bool {
        ids.allSatisfy { UUID(uuidString: $0) != nil } && Set(ids).count == ids.count
    }

    private static func displayable(_ value: String) -> Bool {
        !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private static func decodeContent<T: Decodable>(_ type: T.Type, _ value: DecodedJSONValue) -> T? {
        guard let data = try? value.exactJSONData() else { return nil }
        return try? JSONDecoder.contentFact.decode(type, from: data)
    }
}

private extension HackerNewsReferences? {
    var isValid: Bool {
        guard let references = self else { return true }
        return references.storyCount >= 0 && references.commentCount >= 0
            && (references.stories?.count ?? 0) <= 100 && (references.comments?.count ?? 0) <= 100
    }
}

private struct PostRow: Decodable {
    let id: String
    let title: String
    let hackerNewsReferences: HackerNewsReferences?
}

private struct RssRow: Decodable {
    let id: String
    let title: String
    let rssFeedTitle: String
    let hackerNewsReferences: HackerNewsReferences?
}

private struct PostSearchEnvelope: Decodable {
    let success: Bool
    let results: [PostRow]
}

private struct PostDetailsEnvelope: Decodable {
    let success: Bool
    let post: PostRow
}

private struct RssListEnvelope: Decodable {
    let success: Bool
    let results: [RssRow]
}

private struct RssDetailsEnvelope: Decodable {
    let success: Bool
    let rssFeedItem: RssRow
}

private struct StoryEnvelope: Decodable {
    let success: Bool
    let items: [RssRow]
}

private extension JSONDecoder {
    static let contentFact: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let string = try container.decode(String.self)
            let fractional = try? Date(string, strategy: Date.ISO8601FormatStyle(includingFractionalSeconds: true))
            let whole = try? Date(string, strategy: Date.ISO8601FormatStyle(includingFractionalSeconds: false))
            if let date = fractional ?? whole { return date }
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot parse date")
        }
        return decoder
    }()
}

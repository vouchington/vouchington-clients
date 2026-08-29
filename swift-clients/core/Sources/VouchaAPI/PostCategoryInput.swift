import Foundation
import VouchaModels

public enum PostCategoryInput: Encodable, Equatable, Sendable {
    case topic(topicId: String)
    case hashtag(String)

    public init(topicId: String) {
        self = .topic(topicId: topicId)
    }

    public init?(hashtag: String) {
        let authored = hashtag.trimmingCharacters(in: .whitespacesAndNewlines)
        guard CanonicalHashtagSlug(rawValue: authored) != nil else { return nil }
        self = .hashtag(authored)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case let .topic(topicId):
            let normalizedTopicId = topicId.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !normalizedTopicId.isEmpty else {
                throw EncodingError.invalidValue(
                    topicId,
                    EncodingError.Context(
                        codingPath: encoder.codingPath,
                        debugDescription: "A topic category requires a topic ID."
                    )
                )
            }
            try container.encode("topic", forKey: .type)
            try container.encode(normalizedTopicId, forKey: .topicId)
        case let .hashtag(hashtag):
            let authored = hashtag.trimmingCharacters(in: .whitespacesAndNewlines)
            guard CanonicalHashtagSlug(rawValue: authored) != nil else {
                throw EncodingError.invalidValue(
                    hashtag,
                    EncodingError.Context(
                        codingPath: encoder.codingPath,
                        debugDescription: "A hashtag category requires a valid canonical hashtag slug."
                    )
                )
            }
            try container.encode("hashtag", forKey: .type)
            try container.encode(authored, forKey: .hashtag)
        }
    }

    private enum CodingKeys: String, CodingKey {
        case type
        case topicId = "topic_id"
        case hashtag
    }
}

import Foundation
import VouchaModels

private enum EntityRelationJSONValue: Encodable {
    case string(String)

    func encode(to encoder: Encoder) throws {
        switch self {
        case let .string(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        }
    }
}

private struct EntityRelationCreateBody: Encodable {
    let objectId: String

    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(["objectId": EntityRelationJSONValue.string(objectId)])
    }
}

public extension Endpoint {
    static func topics(
        query: String? = nil,
        limit: Int = 25,
        topicTypes: [String]? = nil,
        sort: String? = nil,
        after: String? = nil,
        spendingCategory: Bool? = nil,
        rssFeed: Bool? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let query {
            items.append(.init(name: "q", value: query))
        }
        if let topicTypes, !topicTypes.isEmpty {
            items.append(.init(name: "topic_types", value: topicTypes.joined(separator: ",")))
        }
        if let sort {
            items.append(.init(name: "sort", value: sort))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let spendingCategory {
            items.append(.init(name: "spending_category", value: spendingCategory ? "true" : "false"))
        }
        if let rssFeed {
            items.append(.init(name: "rss_feed", value: rssFeed ? "true" : "false"))
        }
        return Endpoint(.GET, path: "/api/v1/topics", queryItems: items)
    }

    static func posts(query: String? = nil, limit: Int = 25, after: String? = nil) -> Endpoint {
        var items: [URLQueryItem] = [
            .init(name: "limit", value: "\(limit)"),
            .init(name: "sort", value: "relevance")
        ]
        if let query {
            items.append(.init(name: "q", value: query))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/posts", queryItems: items)
    }

    static func urls(query: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let query {
            items.append(.init(name: "query", value: query))
        }
        return Endpoint(.GET, path: "/api/v1/urls", queryItems: items)
    }

    static func publisherTypes() -> Endpoint {
        Endpoint(.GET, path: "/api/v1/topics/publisher-types")
    }

    static func userTags() -> Endpoint {
        Endpoint(.GET, path: "/api/v1/topics/user-tags")
    }

    static func entityRelations(
        entityType: String,
        entityId: String,
        predicate: String,
        objectType: String,
        sort: String? = nil,
        limit: Int? = nil,
        after: String? = nil,
        positiveNetVoteScore: Bool? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let sort {
            items.append(.init(name: "sort", value: sort))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let positiveNetVoteScore {
            items.append(.init(name: "positiveNetVoteScore", value: positiveNetVoteScore ? "true" : "false"))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/entity-relations/\(entityType)/\(pathSegment(entityId))/\(predicate)/\(objectType)",
            queryItems: items
        )
    }

    static func createEntityRelation(
        entityType: String,
        entityId: String,
        predicate: String,
        objectType: String,
        objectId: String
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/entity-relations/\(entityType)/\(pathSegment(entityId))/\(predicate)/\(objectType)",
            body: EntityRelationCreateBody(objectId: objectId)
        )
    }

    static func voteEntityRelation(relationId: String, choice: ElectionVoteChoice) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/entity-relations/\(pathSegment(relationId))/vote",
            body: ["choice": choice.rawValue]
        )
    }

    static func clearEntityRelationVote(relationId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/entity-relations/\(pathSegment(relationId))/vote")
    }
}

import Foundation
import VouchaModels

public enum NullableStringPatchField: Encodable, Equatable, Sendable {
    case null
    case value(String)

    public init(_ value: String) {
        self = .value(value)
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .null:
            try container.encodeNil()
        case let .value(value):
            try container.encode(value)
        }
    }
}

public struct UpdateTopicBody: Encodable, Sendable {
    public let name: String?
    public let slug: String?
    public let markdown: String?
    public let topicType: String?
    public let noindex: Bool?
    public let allowReviews: Bool?
    public let hostname: NullableStringPatchField?
    public let logoImageId: NullableStringPatchField?
    public let heroImageId: NullableStringPatchField?

    public init(
        name: String? = nil,
        slug: String? = nil,
        markdown: String? = nil,
        topicType: String? = nil,
        noindex: Bool? = nil,
        allowReviews: Bool? = nil,
        hostname: NullableStringPatchField? = nil,
        logoImageId: NullableStringPatchField? = nil,
        heroImageId: NullableStringPatchField? = nil
    ) {
        self.name = name
        self.slug = slug
        self.markdown = markdown
        self.topicType = topicType
        self.noindex = noindex
        self.allowReviews = allowReviews
        self.hostname = hostname
        self.logoImageId = logoImageId
        self.heroImageId = heroImageId
    }
}

private struct TopicAliasBody: Encodable {
    let aliases: String
}

private struct TopicMergeBody: Encodable {
    let destinationIdOrSlug: String
}

public extension Endpoint {
    static func topic(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/topics/\(pathSegment(id))")
    }

    static func topics(query: String, topicTypes: [String]? = nil, limit: Int? = nil) -> Endpoint {
        var items = [URLQueryItem(name: "q", value: query)]
        if let topicTypes, !topicTypes.isEmpty {
            items.append(.init(name: "topic_types", value: topicTypes.joined(separator: ",")))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        return Endpoint(.GET, path: "/api/v1/topics", queryItems: items)
    }

    static func createTopic(body: CreateTopicBody) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/topics", body: body)
    }

    static func voteTopic(topicId: String, choice: ElectionVoteChoice) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/topics/\(pathSegment(topicId))/vote", body: ["choice": choice.rawValue])
    }

    static func clearTopicVote(topicId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/topics/\(pathSegment(topicId))/vote")
    }

    static func updateTopic(id: String, body: UpdateTopicBody) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/topics/\(pathSegment(id))", body: body)
    }

    static func topicTypeAttributes(topicId: String, typeSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/topics/\(pathSegment(topicId))/\(pathSegment(typeSlug))")
    }

    static func updateTopicTypeAttributes(
        topicId: String,
        typeSlug: String,
        body: some Encodable & Sendable
    ) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/topics/\(pathSegment(topicId))/\(pathSegment(typeSlug))", body: body)
    }

    static func topicAliases(topicId: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        return Endpoint(.GET, path: "/api/v1/topics/\(pathSegment(topicId))/aliases", queryItems: items)
    }

    static func createTopicAliases(topicId: String, aliases: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/topics/\(pathSegment(topicId))/aliases",
            body: TopicAliasBody(aliases: aliases)
        )
    }

    static func deleteTopicAlias(topicId: String, aliasId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/topics/\(pathSegment(topicId))/aliases/\(pathSegment(aliasId))")
    }

    static func mergeTopicAliases(sourceTopicId: String, destinationIdOrSlug: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/topics/\(pathSegment(sourceTopicId))/merges",
            body: TopicMergeBody(destinationIdOrSlug: destinationIdOrSlug)
        )
    }

    static func topicAdditionalHostnames(topicId: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        return Endpoint(.GET, path: "/api/v1/topics/\(pathSegment(topicId))/additional-hostnames", queryItems: items)
    }

    static func createTopicAdditionalHostname(topicId: String, hostname: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/topics/\(pathSegment(topicId))/additional-hostnames",
            body: ["hostname": hostname]
        )
    }

    static func deleteTopicAdditionalHostname(topicId: String, hostnameId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/topics/\(pathSegment(topicId))/additional-hostnames/\(pathSegment(hostnameId))"
        )
    }

    static func spendingCategoryAttributes(topicId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/topics/\(pathSegment(topicId))/spending-category")
    }

    static func updateSpendingCategoryAttributes(topicId: String, body: some Encodable & Sendable) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/topics/\(pathSegment(topicId))/spending-category", body: body)
    }

    static func referralProgramValidationInfo(topicId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/topics/\(pathSegment(topicId))/referral-program/validation-info")
    }
}

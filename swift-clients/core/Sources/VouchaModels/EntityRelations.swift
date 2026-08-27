import Foundation

public struct EntityRelation: Codable, Identifiable, Sendable {
    public let id: String
    public let subjectId: String?
    public let objectId: String?
    public let createdAt: Date
    public let createdById: String
    public let deletedAt: Date?
    public let deletedById: String?
    public let orderIndex: Int?
    public let votesCountUp: Int?
    public let votesCountDown: Int?
    public let votesScoreNet: Double?
    public let votesScoreSort: Double?
    public let objectData: EntityRelationObjectData
}

public struct EntityRelationObjectData: Codable, Sendable {
    public let id: String?
    public let name: String?
    public let slug: String?
    public let title: String?
    public let url: String?
    public let pathname: String?
    public let username: String?
    public let postType: String?
    public let topicType: String?
    public let feedType: String?
    public let hostname: EntityRelationHostname?
    public let latestCrawl: EntityRelationLatestCrawl?

    public var displayTitle: String? {
        title ?? name ?? username ?? hostname?.hostname ?? url ?? slug ?? id
    }

    public var displayDetail: String? {
        pathname ?? topicType ?? postType ?? feedType ?? slug ?? id
    }
}

public struct EntityRelationHostname: Codable, Sendable {
    public let id: String?
    public let hostname: String
}

public struct EntityRelationLatestCrawl: Codable, Sendable {
    public let title: String?
    public let imageUrl: String?
}

public struct EntityRelationRef: Codable, Identifiable, Sendable {
    public let id: String
}

public struct EntityRelationVote: Codable, Sendable {
    public let entityType: String
    public let entityId: String
    public let userId: String
    public let choice: ElectionVoteChoice
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case entityId, userId, choice, createdAt
    }
}

public struct EntityRelationsResponse: Codable, Sendable {
    public let results: [EntityRelationRef]
    public let pageInfo: EntityRelationsPageInfo
    public let entityRelations: [String: EntityRelation]
    public let entityRelationElections: [String: EntityRelationElection]?
    public let electionVotes: [String: EntityRelationVote]?
}

public struct EntityRelationResponse: Codable, Sendable {
    public let relation: EntityRelation
}

public struct EntityRelationsPageInfo: Codable, Sendable {
    public let hasNextPage: Bool
    public let hasPreviousPage: Bool?
    public let endCursor: String?
    public let startCursor: String?
}

public struct EntityRelationElection: Codable, Sendable {
    public let id: String
    public let votesScoreNet: Double
    public let votesCountUp: Int
    public let votesCountDown: Int
}

public struct PublisherTypeTopic: Codable, Identifiable, Sendable {
    public let id: String
    public let slug: String
    public let label: String
}

public struct PublisherTypesResponse: Codable, Sendable {
    public let publisherTypes: [PublisherTypeTopic]
}

public struct UserTagsResponse: Codable, Sendable {
    public let userTags: [PublisherTypeTopic]
}

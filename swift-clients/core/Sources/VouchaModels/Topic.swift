import Foundation

public struct Topic: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let name: String
    public let slug: String
    public let markdown: String?
    public let topicType: String
    public let aliases: [String]?
    public let allowReviews: Bool?
    public let noindex: Bool?
    @RequiredNullable
    public var hostnameId: String?
    @RequiredNullable
    public var hostname: TopicHostname?
    @RequiredNullable
    public var logoImageId: String?
    @RequiredNullable
    public var heroImageId: String?
    @RequiredNullable
    public var homepageUrlId: String?
    @RequiredNullable
    public var linguaRsDetectedLanguage: String?
    @RequiredNullable
    public var referralProgramId: String?
    @RequiredNullable
    public var referralProgramSlug: String?
    @RequiredNullable
    public var rewardsProgramId: String?
    public let createdAt: Date?
    public let createdBy: PublicUser?
    public let updatedBy: PublicUser?
    public let election: TopicElection?

    public init(
        id: String,
        name: String,
        slug: String,
        markdown: String?,
        topicType: String,
        aliases: [String]? = nil,
        allowReviews: Bool? = nil,
        noindex: Bool? = nil,
        hostnameId: String? = nil,
        hostname: TopicHostname? = nil,
        logoImageId: String? = nil,
        heroImageId: String? = nil,
        createdAt: Date,
        createdBy: PublicUser? = nil,
        updatedBy: PublicUser? = nil,
        election: TopicElection? = nil
    ) {
        entityType = nil
        self.id = id
        self.name = name
        self.slug = slug
        self.markdown = markdown
        self.topicType = topicType
        self.aliases = aliases
        self.allowReviews = allowReviews
        self.noindex = noindex
        self.hostnameId = hostnameId
        self.hostname = hostname
        self.logoImageId = logoImageId
        self.heroImageId = heroImageId
        homepageUrlId = nil
        linguaRsDetectedLanguage = nil
        referralProgramId = nil
        referralProgramSlug = nil
        rewardsProgramId = nil
        self.createdAt = createdAt
        self.createdBy = createdBy
        self.updatedBy = updatedBy
        self.election = election
    }

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, name, slug, markdown, topicType, aliases, allowReviews, noindex, hostnameId, hostname
        case logoImageId, heroImageId, homepageUrlId, linguaRsDetectedLanguage, referralProgramId
        case referralProgramSlug, rewardsProgramId, createdAt, createdBy, updatedBy, election
    }
}

public struct TopicHostname: Codable, Identifiable, Sendable {
    public let id: String
    public let hostname: String
    public let topicId: String?
}

public struct TopicReference: Codable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String
    public let topicType: String
    public let createdBy: PublicUser?
    public let updatedBy: PublicUser?
}

public struct TopicEnvelope: Codable, Sendable {
    public let topic: Topic
}

public struct TopicSearchResult: Codable, Identifiable, Sendable {
    public let entityType: String
    public let id: String
    public let name: String?
    public let slug: String?
    public let topicType: String?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, name, slug, topicType
    }
}

public struct TopicSearchResponse: Codable, Sendable {
    public let results: [TopicSearchResult]
    public let pageInfo: Page<TopicSearchResult>.PageInfo
    public let topics: [String: Topic]
    public let topicMetrics: [String: DecodedJSONValue]?
    public let topicElections: [String: RssFeedElectionSummary]?
    public let electionVotes: [String: ElectionVote]?

    enum CodingKeys: String, CodingKey {
        case results
        case pageInfo
        case topics
        case topicMetrics = "topicsMetrics"
        case topicElections
        case electionVotes
    }
}

public struct TopicAdditionalHostname: Codable, Identifiable, Sendable {
    public let hostnameId: String
    public let hostname: String
    public let topicId: String?
    public let createdAt: Date?

    public var id: String {
        hostnameId
    }

    public init(id: String, hostname: String, topicId: String? = nil, createdAt: Date? = nil) {
        hostnameId = id
        self.hostname = hostname
        self.topicId = topicId
        self.createdAt = createdAt
    }
}

public struct TopicAlias: Codable, Identifiable, Sendable, Equatable {
    public let id: String
    public let alias: String
    public let topicId: String?

    public init(id: String, alias: String, topicId: String?) {
        self.id = id
        self.alias = alias
        self.topicId = topicId
    }
}

public struct TopicAdditionalHostnamesResponse: Codable, Sendable {
    public let results: [TopicAdditionalHostname]
    public let pageInfo: Page<TopicAdditionalHostname>.PageInfo

    public init(results: [TopicAdditionalHostname], pageInfo: Page<TopicAdditionalHostname>.PageInfo) {
        self.results = results
        self.pageInfo = pageInfo
    }
}

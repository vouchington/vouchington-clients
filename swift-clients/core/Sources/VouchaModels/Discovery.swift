import Foundation

public enum RecommendedTopicsSort: String, Codable, Sendable {
    case score, best
}

public struct WebSearchResponse: Codable, Sendable {
    public let results: [WebSearchResult]
    public let pageInfo: Page<WebSearchResult>.PageInfo
}

public struct WebSearchResult: Codable, Identifiable, Sendable {
    public let url: WebSearchURL
    public let snippet: String?
    public let matchType: String

    public var id: String {
        url.id
    }
}

public struct WebSearchURL: Codable, Identifiable, Sendable {
    public let id: String
    public let hostname: WebSearchHostname
    public let canonicalUrlId: String?
    public let url: String
    public let pathname: String
    public let searchParams: [String: String]
}

public struct WebSearchHostname: Codable, Identifiable, Sendable {
    public let id: String
    public let hostname: String
    public let topicId: String?
}

public struct FediverseSearchResponse: Codable, Sendable {
    public let buckets: [FediverseSearchBucket]
}

public struct FediverseSearchBucket: Codable, Sendable {
    public let provider: String
    public let status: String
    public let items: [FediverseSearchResult]
    public let nextCursor: String?
    public let errorCode: String?
}

public struct FediverseSearchResult: Codable, Identifiable, Sendable {
    public let provider: String
    public let resultType: String
    public let sourceHostname: String?
    public let externalUrl: String
    public let title: String
    public let summary: String
    public let authorName: String?
    public let authorUrl: String?
    public let publishedAt: Date?
    public let thumbnailUrl: String?

    public var id: String {
        externalUrl
    }
}

public struct TrendingCommunity: Codable, Identifiable, Sendable {
    public let id: String
    public let trendingScore: Double
    public let memberCount: Int
    public let postCount: Int
    public let virtualSubscriptionCount: Int
}

public struct TrendingCommunitiesResponse: Codable, Sendable {
    public let communities: [TrendingCommunity]
    public let pageInfo: Page<TrendingCommunity>.PageInfo
}

public struct TrendingReferralProgram: Codable, Identifiable, Sendable {
    public let id: String
    public let trendingScore: Double
    public let linkCount: Int
}

public struct TrendingReferralProgramsResponse: Codable, Sendable {
    public let referralPrograms: [TrendingReferralProgram]
    public let pageInfo: Page<TrendingReferralProgram>.PageInfo

    private enum CodingKeys: String, CodingKey {
        case referralPrograms
        case results
        case pageInfo
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        if let referralPrograms = try container.decodeIfPresent(
            [TrendingReferralProgram].self,
            forKey: .referralPrograms
        ) {
            self.referralPrograms = referralPrograms
        } else {
            referralPrograms = try container.decode([TrendingReferralProgram].self, forKey: .results)
        }
        pageInfo = try container.decode(Page<TrendingReferralProgram>.PageInfo.self, forKey: .pageInfo)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(referralPrograms, forKey: .referralPrograms)
        try container.encode(pageInfo, forKey: .pageInfo)
    }
}

public struct RecommendedTopicResult: Codable, Identifiable, Sendable {
    public let id: String
    public let score: Double
    public let reason: String
}

public struct RecommendedTopicsResponse: Codable, Sendable {
    public let results: [RecommendedTopicResult]
    public let pageInfo: Page<RecommendedTopicResult>.PageInfo
}

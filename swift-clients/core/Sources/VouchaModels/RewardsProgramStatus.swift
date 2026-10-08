import Foundation

public struct RewardsProgramStatus: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let rewardsProgramStatusId: String
    public let since: LocalDate?
    public let until: LocalDate?
    public let rewardsProgramStatus: RewardsProgramSummary

    public init(
        id: String,
        rewardsProgramStatusId: String,
        since: LocalDate? = nil,
        until: LocalDate? = nil,
        rewardsProgramStatus: RewardsProgramSummary
    ) {
        self.id = id
        self.rewardsProgramStatusId = rewardsProgramStatusId
        self.since = since
        self.until = until
        self.rewardsProgramStatus = rewardsProgramStatus
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        rewardsProgramStatusId = try container.decode(String.self, forKey: .rewardsProgramStatusId)
        since = try container.decodeIfPresent(LocalDate.self, forKey: .since)
        until = try container.decodeIfPresent(LocalDate.self, forKey: .until)
        rewardsProgramStatus = try container.decode(RewardsProgramSummary.self, forKey: .rewardsProgramStatus)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(rewardsProgramStatusId, forKey: .rewardsProgramStatusId)
        try container.encode(since, forKey: .since)
        try container.encode(until, forKey: .until)
        try container.encode(rewardsProgramStatus, forKey: .rewardsProgramStatus)
    }

    private enum CodingKeys: String, CodingKey {
        case id, rewardsProgramStatus
        case rewardsProgramStatusId = "rewardsProgramStatusTopicId"
        case since = "startedOn"
        case until = "expiresOn"
    }
}

public struct RewardsProgramStatusPage: Codable, Sendable {
    public let results: [RewardsProgramStatus]
    public let pageInfo: Page<RewardsProgramStatus>.PageInfo

    public init(
        results: [RewardsProgramStatus],
        pageInfo: Page<RewardsProgramStatus>.PageInfo = .init(hasNextPage: false)
    ) {
        self.results = results
        self.pageInfo = pageInfo
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        results = try container.decode([RewardsProgramStatus].self, forKey: .results)
        pageInfo = try container.decodeIfPresent(
            Page<RewardsProgramStatus>.PageInfo.self,
            forKey: .pageInfo
        ) ?? .init(hasNextPage: false)
    }

    private enum CodingKeys: String, CodingKey {
        case results, pageInfo
    }
}

public struct RewardsProgramStatusResponse: Codable, Sendable {
    public let rewardsProgramStatus: RewardsProgramStatus
}

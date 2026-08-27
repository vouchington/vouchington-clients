import Foundation

public struct RewardsProgramSummary: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String

    public init(id: String, name: String, slug: String) {
        self.id = id
        self.name = name
        self.slug = slug
    }
}

public struct PointValuation: Codable, Equatable, Identifiable, Sendable {
    public static let maximumValuePerPointAmount: Int64 = 9_999_999_999

    public let id: String
    public let rewardsProgramId: String
    public let valuePerPoint: ScaledMoney
    public let note: String?
    public let rewardsProgram: RewardsProgramSummary

    public init(
        id: String,
        rewardsProgramId: String,
        valuePerPoint: ScaledMoney,
        note: String?,
        rewardsProgram: RewardsProgramSummary
    ) {
        self.id = id
        self.rewardsProgramId = rewardsProgramId
        self.valuePerPoint = valuePerPoint
        self.note = note
        self.rewardsProgram = rewardsProgram
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        rewardsProgramId = try container.decode(String.self, forKey: .rewardsProgramId)
        valuePerPoint = try container.decode(ScaledMoney.self, forKey: .valuePerPoint)
        note = try container.decodeIfPresent(String.self, forKey: .note)
        rewardsProgram = try container.decode(RewardsProgramSummary.self, forKey: .rewardsProgram)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(rewardsProgramId, forKey: .rewardsProgramId)
        try container.encode(valuePerPoint, forKey: .valuePerPoint)
        try container.encode(note, forKey: .note)
        try container.encode(rewardsProgram, forKey: .rewardsProgram)
    }

    private enum CodingKeys: String, CodingKey {
        case id, rewardsProgramId, valuePerPoint, note, rewardsProgram
    }
}

public struct PointValuationPage: Codable, Sendable {
    public let results: [PointValuation]
    public let pageInfo: Page<PointValuation>.PageInfo

    public init(
        results: [PointValuation],
        pageInfo: Page<PointValuation>.PageInfo = .init(hasNextPage: false)
    ) {
        self.results = results
        self.pageInfo = pageInfo
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        results = try container.decode([PointValuation].self, forKey: .results)
        pageInfo = try container.decodeIfPresent(
            Page<PointValuation>.PageInfo.self,
            forKey: .pageInfo
        ) ?? .init(hasNextPage: false)
    }

    private enum CodingKeys: String, CodingKey {
        case results, pageInfo
    }
}

public struct PointValuationResponse: Codable, Sendable {
    public let pointValuation: PointValuation
}

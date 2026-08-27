import Foundation

public enum ModerationTransparencyMetric: String, Codable, Sendable {
    case appeals
    case automatedModeration = "automated_moderation"
    case moderationActions = "moderation_actions"
    case reports
    case unknown

    public init(from decoder: Decoder) throws {
        let rawValue = try decoder.singleValueContainer().decode(String.self)
        self = Self(rawValue: rawValue) ?? .unknown
    }
}

public enum ModerationTransparencyRange: String, Codable, Sendable {
    case today
    case days7 = "7d"
    case days30 = "30d"
    case days90 = "90d"
    case all
    case unknown

    public init(from decoder: Decoder) throws {
        let rawValue = try decoder.singleValueContainer().decode(String.self)
        self = Self(rawValue: rawValue) ?? .unknown
    }
}

public struct ModerationTransparencyBucket: Codable, Sendable {
    public let date: String
    public let metric: ModerationTransparencyMetric
    public let category: String
    public let count: Int
}

public struct ModerationTransparency: Codable, Sendable {
    public let range: ModerationTransparencyRange
    public let buckets: [ModerationTransparencyBucket]
    public let nextCursor: String?

    enum CodingKeys: String, CodingKey {
        case range, buckets
        case nextCursor
    }
}

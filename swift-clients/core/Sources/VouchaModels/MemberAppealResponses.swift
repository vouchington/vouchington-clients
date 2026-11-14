import Foundation

public struct MemberAppealReadResponse: Codable, Sendable {
    public let appeal: ModerationAppealReadProjection
}

public struct MemberAppealListResponse: Codable, Sendable {
    public let appeals: [ModerationAppealReadProjection]
    public let pageInfo: Page<ModerationAppealReadProjection>.PageInfo
}

public struct MemberAppealSubmissionResponse: Codable, Sendable {
    public let appeal: ModerationAppealReadProjection
    public let isDuplicate: Bool

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode([
            "appeal": SubmissionField.appeal(appeal),
            "isDuplicate": .duplicate(isDuplicate)
        ])
    }

    private enum SubmissionField: Encodable {
        case appeal(ModerationAppealReadProjection)
        case duplicate(Bool)

        func encode(to encoder: any Encoder) throws {
            var container = encoder.singleValueContainer()
            switch self {
            case let .appeal(value): try container.encode(value)
            case let .duplicate(value): try container.encode(value)
            }
        }
    }
}

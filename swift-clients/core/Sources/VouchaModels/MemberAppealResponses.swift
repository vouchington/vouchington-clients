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
}

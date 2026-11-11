import Foundation

public struct TopicDetailResponse: Codable, Sendable {
    public let topic: Topic
    public let html: String
    public let topicMetrics: DecodedJSONValue
    public let topicElection: DecodedJSONValue
    public let electionVote: DecodedJSONValue
    public let topicCategories: DecodedJSONValue
    public let topicChildren: DecodedJSONValue
    public let topicParents: DecodedJSONValue
    public let topicContentUpdate: DecodedJSONValue
    /// Present only when `topic.topicType == "referral_program"`; `nil` for other types or a missing extension row.
    public let referralProgramAttributes: ReferralProgramAttributes?
}

public struct ReferralProgramAttributes: Codable, Equatable, Sendable {
    public let companyTopicId: String?
    public let enabledAt: Date?
    public let disabledAt: Date?
}

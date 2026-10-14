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
}

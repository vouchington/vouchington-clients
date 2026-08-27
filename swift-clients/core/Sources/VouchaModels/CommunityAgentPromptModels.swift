import Foundation

public struct CommunityAgentPrompt: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let createdById: String
    public let slotAllocated: Bool
    public let onFlagAction: CommunityOnFlagAction
    public let activatedAt: Date?
    public let deactivatedAt: Date?
    public let deletedAt: Date?
    public let deletedById: String?
    public let agentId: String
    public let prompt: String
    public let modelName: String
    public let modelProvider: String
    public let createdAt: Date
    public let updatedAt: Date
}

public struct CommunityAgentPromptSlotInfo: Codable, Sendable {
    public let used: Int?
    public let limit: Int?
    public let remaining: Int?
    public let limitsByPlan: [String: Int]?
}

public enum CommunityAgentPromptHistoryAction: String, Codable, Sendable {
    case created
    case updated
    case deleted
    case allocated
    case deallocated
    case deactivated
}

public struct CommunityAgentPromptHistoryChangedBy: Codable, Sendable {
    public let id: String
    public let username: String?
}

public struct CommunityAgentPromptFieldChange: Codable, Sendable {
    public let previous: DecodedJSONValue
    public let next: DecodedJSONValue
}

public struct CommunityAgentPromptHistoryEntry: Codable, Identifiable, Sendable {
    public let id: String
    public let agentPromptId: String
    public let communityId: String
    public let action: CommunityAgentPromptHistoryAction
    public let changedBy: CommunityAgentPromptHistoryChangedBy?
    public let previousFields: [String: DecodedJSONValue]
    public let nextFields: [String: DecodedJSONValue]
    public let changedFields: [String: CommunityAgentPromptFieldChange]
    public let createdAt: Date
}

public enum CommunityAutomodActionSourceType: String, Codable, Sendable {
    case agentModeration = "agent_moderation"
    case openAIOmni = "openai_omni"
    case spamDetection = "spam_detection"
    case communityPrompt = "community_prompt"
}

public enum CommunityAutomodActionCurrentState: String, Codable, Sendable {
    case rejected
    case inReview = "in_review"
    case unpublished
}

public enum CommunityAutomodTrainingLabel: String, Codable, Sendable {
    case truePositive = "true_positive"
    case falsePositive = "false_positive"
    case falseNegativeCandidate = "false_negative_candidate"
    case trueNegative = "true_negative"
    case accepted
    case edited
    case rejected
    case notApplicable = "not_applicable"
}

public struct CommunityAutomodAction: Codable, Sendable {
    public let sourceKey: String
    public let sourceType: CommunityAutomodActionSourceType
    public let postId: String
    public let communityId: String
    public let agentModerationId: String?
    public let moderatorSlug: String?
    public let title: String
    public let markdownPreview: String
    public let postType: String
    public let postHref: String
    public let createdAt: Date
    public let actionAt: Date
    public let confidenceScore: Double?
    public let flagged: Bool
    public let reason: String?
    public let categories: [String]
    public let modelOutput: DecodedJSONValue
    public let currentState: CommunityAutomodActionCurrentState
    public let feedbackLabel: CommunityAutomodTrainingLabel?
}

public enum CommunityAutomodFeedbackOutcome: String, Codable, Sendable {
    case falsePositive = "false_positive"
    case truePositive = "true_positive"
}

public enum CommunityAutomodFeedbackAction: String, Codable, Sendable {
    case reinstate
    case keepRemoved = "keep_removed"
    case labelOnly = "label_only"
}

public struct CommunityAutomodFeedbackInput: Codable, Sendable {
    public let outcome: CommunityAutomodFeedbackOutcome
    public let action: CommunityAutomodFeedbackAction
    public let reasonCode: String?
    public let note: String?
}

public struct CommunityAutomodSimulationResult: Codable, Sendable {
    public let postId: String
    public let title: String
    public let postType: String
    public let approvedAt: Date
    public let contentExcerpt: String
    public let flagged: Bool
    public let reason: String
    public let wouldUnpublish: Bool
}

public struct CommunityAutomodFalsePositiveEstimate: Codable, Sendable {
    public let historicalFlaggedCount: Int
    public let historicalApprovedCount: Int
    public let rate: Double?
}

public struct CommunityAutomodSimulation: Codable, Sendable {
    public struct Summary: Codable, Sendable {
        public let promptId: String
        public let timeWindowHours: Int
        public let sampleCount: Int
        public let wouldFlagCount: Int
        public let wouldUnpublishCount: Int
        public let falsePositiveEstimate: CommunityAutomodFalsePositiveEstimate?
    }

    public let simulation: Summary
    public let results: [CommunityAutomodSimulationResult]
}

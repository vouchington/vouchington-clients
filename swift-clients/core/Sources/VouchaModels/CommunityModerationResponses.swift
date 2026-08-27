import Foundation

public struct CommunityAiAgentsResponse: Codable, Sendable {
    public let communityAiAgents: [CommunityAiAgent]
}

public struct CommunityAiAgentResponse: Codable, Sendable {
    public let communityAiAgent: CommunityAiAgent
}

public struct CommunityModeratorStatsResponse: Codable, Sendable {
    public let window: Int
    public let stats: [CommunityModeratorStatEntry]
    public let users: [String: PublicUser]
}

public struct ModeratorVacationResponse: Codable, Sendable {
    public let vacation: CommunityMemberVacation?
    public let suppressCommunityDigestsWhileOnVacation: Bool
}

public struct VacationDigestPreferenceResponse: Codable, Sendable {
    public let suppressCommunityDigestsWhileOnVacation: Bool
}

public struct CommunityModlogResultsResult: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id
    }
}

public struct CommunityModlogResponse: Codable, Sendable {
    public let results: [CommunityModlogResultsResult]
    public let pageInfo: Page<CommunityModlogResultsResult>.PageInfo
    public let moderatorActions: [String: CommunityModlogEntry]
    public let users: [String: PublicUser]
}

public struct CommunityModmailThreadsResponse: Codable, Sendable {
    public let results: [CommunityModmailThread]
    public let pageInfo: Page<CommunityModmailThread>.PageInfo
}

public struct CommunityModmailThreadResponse: Codable, Sendable {
    public let thread: CommunityModmailThread
}

public struct CommunityModmailConversation: Codable, Sendable {
    public let id: String
}

public struct CommunityModmailConversationResponse: Codable, Sendable {
    public let conversation: CommunityModmailConversation
}

public struct CommunityModmailMessageResponse: Codable, Sendable {
    public let message: CommunityModmailMessage
}

public struct CommunityModmailMessagesResponse: Codable, Sendable {
    public let results: [CommunityModmailMessage]
    public let pageInfo: Page<CommunityModmailMessage>.PageInfo
}

public struct CommunitySavedRepliesResponse: Codable, Sendable {
    public let results: [CommunitySavedReply]
    public let pageInfo: Page<CommunitySavedReply>.PageInfo
}

public struct CommunitySavedReplyResponse: Codable, Sendable {
    public let reply: CommunitySavedReply
}

public struct CommunityAgentPromptsResponse: Codable, Sendable {
    public let communityAgentPrompts: [CommunityAgentPrompt]
    public let slotInfo: CommunityAgentPromptSlotInfo
}

public struct CommunityAgentPromptResponse: Codable, Sendable {
    public let communityAgentPrompt: CommunityAgentPrompt
}

public struct CommunityAgentPromptHistoryResponse: Codable, Sendable {
    public let entries: [CommunityAgentPromptHistoryEntry]
    public let nextCursor: String?
}

public struct CommunityAutomodRecentActionStats: Codable, Sendable {
    public let totalCount: Int
    public let falsePositiveCount: Int
    public let falsePositiveRate: Double
}

public struct CommunityAutomodRecentActionsResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunityAutomodRecentActionsResponse: Codable, Sendable {
    public let automodActions: [CommunityAutomodAction]
    public let stats: CommunityAutomodRecentActionStats
    public let pageInfo: Page<CommunityAutomodRecentActionsResult>.PageInfo
}

public struct CommunityAutomodFeedbackResponse: Codable, Sendable {
    public let feedback: DecodedJSONValue?
    public let appliedAction: Bool
}

public struct CommunityOpenAIModeration: Codable, Sendable {
    public let flagged: Bool?
    public let results: DecodedJSONValue?
}

public struct CommunityModerationResultsResponse: Codable, Sendable {
    public let communityAgentModerations: [DecodedJSONValue]
    public let openaiModeration: CommunityOpenAIModeration?
}

public struct CommunityWarning: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let issuedById: String?
    public let issuedByUsername: String?
    public let reason: String?
    public let publicMessage: String?
    public let reportId: String?
    public let userId: String?
    public let communitySlug: String?
    public let createdAt: Date
    @RequiredNullable
    public var revokedAt: Date?
    @RequiredNullable
    public var revokedById: String?
}

public struct CommunityWarningResponse: Codable, Sendable {
    public let warning: CommunityWarning
}

public struct CommunityModerationQueueResponse: Codable, Sendable {
    public let entries: [CommunityModerationQueueEntry]
    public let pageInfo: Page<CommunityModerationQueueEntry>.PageInfo
    public let viewerTier: CommunityModerationQueueViewerTier
}

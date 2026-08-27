import Foundation

public struct AgentModerator: Codable, Sendable, Hashable {
    public let agentId: String
    public let createdAt: Date
    public let updatedAt: Date
}

public struct AgentSummary: Codable, Sendable, Identifiable {
    public let id: String
    public let systemUserId: String
    public let agentType: String
    @RequiredNullable public var activatedAt: Date?
    @RequiredNullable public var deactivatedAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable public var deletedAt: Date?
    public let slug: String?
    public let moderator: AgentModerator?
}

public struct AgentDetailResponse: Codable, Sendable {
    public let agent: AgentSummary
    public let user: PublicUser?
}

public enum AgentConversationFilterKind: String, CaseIterable, Hashable, Sendable {
    case userId = "user_id"
    case username
    case postId = "post_id"
    case postSlug = "post_slug"
    case rssFeedItemId = "rss_feed_item_id"
}

public enum AgentConversationFilter: Hashable, Sendable {
    case userId(String)
    case username(String)
    case postId(String)
    case postSlug(String)
    case rssFeedItemId(String)

    public init?(kind: AgentConversationFilterKind, value: String) {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        self = switch kind {
        case .userId: .userId(trimmed)
        case .username: .username(trimmed)
        case .postId: .postId(trimmed)
        case .postSlug: .postSlug(trimmed)
        case .rssFeedItemId: .rssFeedItemId(trimmed)
        }
    }

    public var kind: AgentConversationFilterKind {
        switch self {
        case .userId: .userId
        case .username: .username
        case .postId: .postId
        case .postSlug: .postSlug
        case .rssFeedItemId: .rssFeedItemId
        }
    }

    public var value: String {
        switch self {
        case let .userId(value), let .username(value), let .postId(value), let .postSlug(value),
             let .rssFeedItemId(value):
            value.trimmingCharacters(in: .whitespacesAndNewlines)
        }
    }

    public var trimmed: AgentConversationFilter? {
        AgentConversationFilter(kind: kind, value: value)
    }
}

public struct AgentListResponse: Codable, Sendable {
    public let results: [AgentSummary]
    public let pageInfo: Page<AgentSummary>.PageInfo
    public let users: [String: PublicUser]
}

public struct AgentConversation: Codable, Sendable, Identifiable {
    public let id: String
    public let channelType: String
    public let title: String
    public let createdAt: Date
    @RequiredNullable public var createdById: String?
    public let updatedAt: Date
    @RequiredNullable public var updatedById: String?
    @RequiredNullable public var deletedAt: Date?
    @RequiredNullable public var deletedById: String?
    @RequiredNullable public var lastResponseId: String?
}

public struct AgentConversationMessageContent: Codable, Sendable {
    public let role: String
    public let content: String?
    public let error: String?
}

public struct AgentConversationMessage: Codable, Sendable, Identifiable {
    public let id: String
    public let conversationId: String
    public let createdAt: Date
    @RequiredNullable public var createdById: String?
    public let updatedAt: Date
    @RequiredNullable public var updatedById: String?
    @RequiredNullable public var deletedAt: Date?
    @RequiredNullable public var deletedById: String?
    public let content: AgentConversationMessageContent?
}

public struct AgentConversationDetailResponse: Codable, Sendable {
    public let conversation: AgentConversation
    public let results: [AgentConversationMessage]
    public let pageInfo: Page<AgentConversationMessage>.PageInfo
}

public struct AgentConversationListResponse: Codable, Sendable {
    public let results: [AgentConversationSummary]
    public let pageInfo: Page<AgentConversationSummary>.PageInfo
    public let users: [String: PublicUser]
}

public struct AgentConversationSummary: Codable, Sendable, Identifiable {
    public let id: String
    public let title: String
    public let createdAt: Date
    @RequiredNullable public var createdById: String?
    public let updatedAt: Date
    @RequiredNullable public var updatedById: String?
    @RequiredNullable public var deletedAt: Date?
    @RequiredNullable public var deletedById: String?
}

import Foundation

public struct ModerationActorSummary: Codable, Sendable {
    public let id: String
    @RequiredNullable
    public var username: String?
    @RequiredNullable
    public var verifiedDisplayName: String?
    @RequiredNullable
    public var profileImageId: String?
}

public struct ModerationAppealCommunitySummary: Codable, Sendable {
    public let id: String
    public let name: String
}

public struct ModerationAppealWarningContext: Codable, Sendable {
    public let id: String
    @RequiredNullable
    public var publicMessage: String?
    @RequiredNullable
    public var community: ModerationAppealCommunitySummary?
    public let createdAt: Date
}

public struct ModerationAppealCommunityBanContext: Codable, Sendable {
    public let id: String
    public let community: ModerationAppealCommunitySummary
    @RequiredNullable
    public var reason: String?
    @RequiredNullable
    public var expiresAt: Date?
    public let createdAt: Date
}

public struct ModerationAppealPostRemovalContext: Codable, Sendable {
    public let id: String
    public let title: String
    @RequiredNullable public var declaredLanguage: String?
    @RequiredNullable public var linguaRsDetectedLanguage: String?
    public let kind: ModerationAppealPostRemovalKind
    @RequiredNullable
    public var community: ModerationAppealCommunitySummary?
    @RequiredNullable
    public var publicReason: String?
    @RequiredNullable
    public var decidedAt: Date?
}

public struct ModerationAppealSuspensionContext: Codable, Sendable {
    public let id: String
    @RequiredNullable
    public var reason: String?
    public let createdAt: Date
}

public enum ModerationAppealTargetContext: Codable, Sendable {
    case warning(ModerationAppealWarningContext)
    case communityBan(ModerationAppealCommunityBanContext)
    case postRemoval(ModerationAppealPostRemovalContext)
    case suspension(ModerationAppealSuspensionContext)

    private enum CodingKeys: String, CodingKey {
        case type
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        switch try container.decode(String.self, forKey: .type) {
        case "warning":
            self = try .warning(ModerationAppealWarningContext(from: decoder))
        case "community_ban":
            self = try .communityBan(ModerationAppealCommunityBanContext(from: decoder))
        case "post_removal":
            self = try .postRemoval(ModerationAppealPostRemovalContext(from: decoder))
        case "suspension":
            self = try .suspension(ModerationAppealSuspensionContext(from: decoder))
        default:
            throw DecodingError.dataCorruptedError(
                forKey: .type,
                in: container,
                debugDescription: "Unknown moderation appeal target context"
            )
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case let .warning(context):
            try container.encode("warning", forKey: .type)
            try context.encode(to: encoder)
        case let .communityBan(context):
            try container.encode("community_ban", forKey: .type)
            try context.encode(to: encoder)
        case let .postRemoval(context):
            try container.encode("post_removal", forKey: .type)
            try context.encode(to: encoder)
        case let .suspension(context):
            try container.encode("suspension", forKey: .type)
            try context.encode(to: encoder)
        }
    }
}

public struct ModerationAppealOriginalDecisionContext: Codable, Sendable {
    @RequiredNullable
    public var actor: ModerationActorSummary?
    @RequiredNullable
    public var internalReason: String?
}

public struct ModerationAppealStaffContext: Codable, Sendable {
    public let appellant: ModerationActorSummary
    public let originalDecision: ModerationAppealOriginalDecisionContext
}

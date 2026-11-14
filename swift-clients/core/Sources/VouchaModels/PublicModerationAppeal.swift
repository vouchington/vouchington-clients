import Foundation

public struct PublicModerationAppeal: Codable, Identifiable, Sendable {
    public let id: String
    @RequiredNullable
    public var userWarningId: String?
    @RequiredNullable
    public var userSuspensionId: String?
    @RequiredNullable
    public var communityBanId: String?
    @RequiredNullable
    public var postId: String?
    @RequiredNullable
    public var communityId: String?
    @RequiredNullable
    public var postRemovalKind: ModerationAppealPostRemovalKind?
    public let status: ModerationAppealStatus
    @RequiredNullable
    public var publicResponse: String?
    @RequiredNullable
    public var approvedAt: Date?
    @RequiredNullable
    public var sentAt: Date?
    @RequiredNullable
    public var resolvedAt: Date?
    @RequiredNullable
    public var resolutionAction: ModerationAppealAction?
    public let createdAt: Date
    public let updatedAt: Date
    public let isOverdue: Bool?
    public let targetContext: ModerationAppealTargetContext?
}

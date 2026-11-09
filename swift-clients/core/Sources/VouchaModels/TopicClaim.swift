import Foundation

public struct TopicClaim: Codable, Identifiable, Sendable {
    public let id: String
    public let topicName: String
    @RequiredNullable
    public var claimantUsername: String?
    @RequiredNullable
    public var claimantDisplayName: String?
    public let topicId: String
    public let claimantUserId: String
    @RequiredNullable
    public var verificationMethod: String?
    public let claimedRole: String
    public let evidence: String
    @RequiredNullable
    public var submittedAt: String?
    @RequiredNullable
    public var verifiedAt: String?
    @RequiredNullable
    public var verifiedById: String?
    @RequiredNullable
    public var rejectedAt: String?
    @RequiredNullable
    public var rejectedById: String?
    @RequiredNullable
    public var rejectionReason: String?
    @RequiredNullable
    public var revokedAt: String?
    @RequiredNullable
    public var revokedById: String?
    @RequiredNullable
    public var revocationReason: String?
    @RequiredNullable
    public var verificationHostnameId: String?
    @RequiredNullable
    public var verificationTokenIssuedAt: String?
    @RequiredNullable
    public var domainVerifiedAt: String?
    public let createdAt: String
    public let updatedAt: String
    public let state: String
}

public struct PendingTopicClaimsResponse: Codable, Sendable {
    public let claims: [TopicClaim]
}

public struct TopicClaimResponse: Codable, Sendable {
    public let claim: TopicClaim
}

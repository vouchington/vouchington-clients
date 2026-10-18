import Foundation

public struct PublicReviewDispute: Codable, Identifiable, Sendable {
    public let id: String
    public let postId: String
    public let topicId: String
    public let reason: ReviewDisputeReason
    public let status: ModerationDisputeStatus
    public let isOverdue: Bool?
    @RequiredNullable
    public var publicResponse: String?
    @RequiredNullable
    public var approvedAt: Date?
    @RequiredNullable
    public var sentAt: Date?
    @RequiredNullable
    public var resolvedAt: Date?
    @RequiredNullable
    public var resolutionAction: ReviewDisputeAction?
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable
    public var postContent: ReviewDisputePostContent?
}

/// The GET contract has authorized member and staff projections of the same dispute.
public enum ModerationDisputeReadProjection: Codable, Identifiable, Sendable {
    case member(PublicReviewDispute)
    case staff(ModerationDispute)

    public var id: String {
        switch self {
        case let .member(dispute): dispute.id
        case let .staff(dispute): dispute.id
        }
    }

    public var postId: String? {
        switch self {
        case let .member(dispute): dispute.postId
        case let .staff(dispute): dispute.postId
        }
    }

    public var status: ModerationDisputeStatus {
        switch self {
        case let .member(dispute): dispute.status
        case let .staff(dispute): dispute.status
        }
    }

    public init(from decoder: any Decoder) throws {
        let staffFields = try decoder.container(keyedBy: ProtectedDisputeField.self)
        let containsStaffField = staffFields.allKeys.contains(where: \.requiresStaffProjection)
        if !containsStaffField {
            self = try .member(PublicReviewDispute(from: decoder))
        } else {
            // Keep the targeted staff identity/claim validation and the generic snapshot's
            // required-nullable checks. A malformed staff object never falls back to member.
            _ = try ReviewDispute(from: decoder)
            self = try .staff(ModerationDispute(from: decoder))
        }
    }

    public func encode(to encoder: any Encoder) throws {
        switch self {
        case let .member(dispute): try dispute.encode(to: encoder)
        case let .staff(dispute): try dispute.encode(to: encoder)
        }
    }
}

public struct ModerationDisputeReadResponse: Codable, Sendable {
    public let dispute: ModerationDisputeReadProjection
}

private enum ProtectedDisputeField: String, CodingKey {
    case disputantUserId, claimText, recommendedAction
    case aiPublicResponse, aiInternalResponse, model, aiDraftedAt, aiDraftRunId
    case internalNotes, draftedAt, editedAt, editedById, approvedById, resolvedById
    case latestLifecycleChangeId, staffContext

    /// Explicit references keep protocol-driven keys visible to static analysis.
    var requiresStaffProjection: Bool {
        switch self {
        case .disputantUserId, .claimText, .recommendedAction,
             .aiPublicResponse, .aiInternalResponse, .model, .aiDraftedAt, .aiDraftRunId,
             .internalNotes, .draftedAt, .editedAt, .editedById, .approvedById, .resolvedById,
             .latestLifecycleChangeId, .staffContext:
            true
        }
    }
}

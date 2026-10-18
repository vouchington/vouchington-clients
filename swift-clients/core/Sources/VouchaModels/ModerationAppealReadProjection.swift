import Foundation

/// Authorized read projections retain their wire privacy boundary on re-encoding.
public enum ModerationAppealReadProjection: Codable, Identifiable, Sendable {
    case member(PublicModerationAppeal)
    case staff(ModerationAppeal)

    public init(from decoder: any Decoder) throws {
        let fields = try decoder.container(keyedBy: ProtectedAppealField.self)
        if fields.allKeys.contains(where: \.requiresStaffProjection) {
            self = try .staff(ModerationAppeal(from: decoder))
        } else {
            self = try .member(PublicModerationAppeal(from: decoder))
        }
    }

    public func encode(to encoder: any Encoder) throws {
        switch self {
        case let .member(appeal): try appeal.encode(to: encoder)
        case let .staff(appeal): try appeal.encode(to: encoder)
        }
    }
}

private enum ProtectedAppealField: String, CodingKey {
    case caseId, appellantUserId, appealReason
    case recommendedAction, aiPublicResponse, aiInternalResponse
    case model, aiDraftedAt, internalNotes
    case draftedAt, editedAt, editedById
    case approvedById, resolvedById, latestLifecycleChangeId
    case staffContext, aiDraftRequestedRunId, aiDraftRunId

    /// Explicit references retain protocol keys in the pinned static analysis.
    var requiresStaffProjection: Bool {
        switch self {
        case .caseId, .appellantUserId, .appealReason,
             .recommendedAction, .aiPublicResponse, .aiInternalResponse,
             .model, .aiDraftedAt, .internalNotes,
             .draftedAt, .editedAt, .editedById,
             .approvedById, .resolvedById, .latestLifecycleChangeId,
             .staffContext, .aiDraftRequestedRunId, .aiDraftRunId:
            true
        }
    }
}

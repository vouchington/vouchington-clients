import Foundation

/// Authorized read projections retain their wire privacy boundary on re-encoding.
public enum ModerationAppealReadProjection: Codable, Identifiable, Sendable {
    case member(PublicModerationAppeal)
    case staff(ModerationAppeal)
    case summary(ModerationAppealSummary)

    public init(from decoder: any Decoder) throws {
        let fields = try decoder.container(keyedBy: ProtectedAppealField.self)
        if fields.contains(.hasDraft) {
            guard !fields.allKeys.contains(where: \.requiresStaffDetail) else {
                throw DecodingError.dataCorrupted(.init(
                    codingPath: decoder.codingPath,
                    debugDescription: "An appeal queue summary cannot contain protected detail fields."
                ))
            }
            self = try .summary(ModerationAppealSummary(from: decoder))
            return
        }
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
        case let .summary(appeal): try appeal.encode(to: encoder)
        }
    }
}

private enum ProtectedAppealField: String, CodingKey {
    case hasDraft
    case caseId, appellantUserId, appealReason
    case recommendedAction, aiPublicResponse, aiInternalResponse
    case model, aiDraftedAt, internalNotes
    case draftedAt, editedAt, editedById
    case approvedById, resolvedById, latestLifecycleChangeId
    case staffContext, aiDraftRequestedRunId, aiDraftRunId

    var requiresStaffDetail: Bool {
        switch self {
        case .hasDraft, .appellantUserId, .recommendedAction: false
        default: true
        }
    }

    /// Explicit references retain protocol keys in the pinned static analysis.
    var requiresStaffProjection: Bool {
        switch self {
        case .hasDraft: false
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

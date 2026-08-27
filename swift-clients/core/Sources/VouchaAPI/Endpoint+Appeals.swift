import Foundation
import VouchaModels

private struct CreateModerationAppealBody: Encodable {
    let targetType: ModerationAppealTargetType
    let targetId: String?
    let appealReason: String
    let postRemovalKind: ModerationAppealPostRemovalKind?
    let cfTurnstileResponse: String?

    enum CodingKeys: String, CodingKey {
        case targetType = "target_type"
        case targetId = "target_id"
        case appealReason = "appeal_reason"
        case postRemovalKind = "post_removal_kind"
        case cfTurnstileResponse = "cf_turnstile_response"
    }
}

private struct UpdateModerationAppealDraftBody: Encodable {
    let publicResponse: String?
    let internalNotes: String?

    enum CodingKeys: String, CodingKey {
        case publicResponse = "public_response"
        case internalNotes = "internal_notes"
    }
}

private struct ResolveModerationAppealBody: Encodable {
    let action: ModerationAppealAction
}

public extension Endpoint {
    static func appeals(
        status: ModerationAppealStatus? = nil,
        limit: Int = 25,
        after: String? = nil,
        mine: Bool = false
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let status {
            items.append(.init(name: "status", value: status.rawValue))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if mine {
            items.append(.init(name: "mine", value: "true"))
        }
        return Endpoint(.GET, path: "/api/v1/appeals", queryItems: items)
    }

    static func appeal(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/appeals/\(pathSegment(id))")
    }

    static func submitAppeal(
        targetType: ModerationAppealTargetType,
        targetId: String,
        appealReason: String,
        postRemovalKind: ModerationAppealPostRemovalKind? = nil,
        turnstileToken: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/appeals",
            body: CreateModerationAppealBody(
                targetType: targetType,
                targetId: targetId,
                appealReason: appealReason,
                postRemovalKind: postRemovalKind,
                cfTurnstileResponse: turnstileToken
            )
        )
    }

    static func submitAppeal(_ request: ModerationAppealSubmissionRequest) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/appeals",
            body: CreateModerationAppealBody(
                targetType: request.targetType,
                targetId: request.targetId,
                appealReason: request.encodedAppealReason,
                postRemovalKind: request.postRemovalKind,
                cfTurnstileResponse: request.turnstileToken
            )
        )
    }

    static func updateAppeal(
        id: String,
        publicResponse: String? = nil,
        internalNotes: String? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/appeals/\(pathSegment(id))",
            body: UpdateModerationAppealDraftBody(
                publicResponse: publicResponse,
                internalNotes: internalNotes
            )
        )
    }

    static func appealApproval(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/appeals/\(pathSegment(id))/approval")
    }

    static func appealDelivery(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/appeals/\(pathSegment(id))/delivery")
    }

    static func appealResolution(id: String, action: ModerationAppealAction) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/appeals/\(pathSegment(id))/resolution",
            body: ResolveModerationAppealBody(action: action)
        )
    }

    static func appealResolutionDrafts(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/appeals/\(pathSegment(id))/resolution-drafts")
    }
}

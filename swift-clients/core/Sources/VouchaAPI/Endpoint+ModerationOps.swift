import Foundation
import VouchaModels

private struct ReviewDisputeDraftBody: Encodable {
    let publicResponse: String?
    let internalNotes: String?

    enum CodingKeys: String, CodingKey {
        case publicResponse = "public_response"
        case internalNotes = "internal_notes"
    }
}

private struct ResolveReviewDisputeBody: Encodable {
    let action: ReviewDisputeResolutionAction
    let bodyText: String?

    enum CodingKeys: String, CodingKey {
        case action
        case bodyText = "body_text"
    }
}

private struct ClearanceStatusBody: Encodable {
    let status: PostClearanceAction
    let reasonCode: String

    enum CodingKeys: String, CodingKey {
        case status
        case reasonCode = "reason_code"
    }
}

private struct ModerationRevealBody: Encodable {
    let postId: String?
    let reportId: String?
    let surface: ModerationRevealSurface

    enum CodingKeys: String, CodingKey {
        case postId
        case reportId
        case surface
    }
}

public extension Endpoint {
    static func adminReviewQueue(after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/posts/review-queue", queryItems: items)
    }

    static func updatePostClearance(postId: String, status: PostClearanceAction) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/posts/\(pathSegment(postId))/clearances",
            body: ClearanceStatusBody(status: status, reasonCode: status.staffReasonCode)
        )
    }

    static func moderationExposure() -> Endpoint {
        Endpoint(.GET, path: "/api/v1/moderation/exposure")
    }

    static func recordModerationReveal(
        postId: String? = nil,
        reportId: String? = nil,
        surface: ModerationRevealSurface
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/moderation/reveals",
            body: ModerationRevealBody(postId: postId, reportId: reportId, surface: surface),
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func adminModlog(
        after: String? = nil,
        communityId: String? = nil,
        actorId: String? = nil,
        actionType: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let communityId {
            items.append(.init(name: "community_id", value: communityId))
        }
        if let actorId {
            items.append(.init(name: "actor_id", value: actorId))
        }
        if let actionType {
            items.append(.init(name: "action_type", value: actionType))
        }
        return Endpoint(.GET, path: "/api/v1/admin/modlog", queryItems: items)
    }

    static func adminModerationAnalytics(range: String? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let range {
            items.append(.init(name: "range", value: range))
        }
        return Endpoint(.GET, path: "/api/v1/admin/moderation-analytics", queryItems: items)
    }

    static func moderationTransparency(range: String? = nil, after: String? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let range {
            items.append(.init(name: "range", value: range))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/moderation-transparency", queryItems: items)
    }

    static func dispute(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/disputes/\(pathSegment(id))")
    }

    static func updateDisputeDraft(
        id: String,
        publicResponse: String? = nil,
        internalNotes: String? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/disputes/\(pathSegment(id))",
            body: ReviewDisputeDraftBody(publicResponse: publicResponse, internalNotes: internalNotes)
        )
    }

    static func disputeApproval(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/disputes/\(pathSegment(id))/approval")
    }

    static func disputeDelivery(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/disputes/\(pathSegment(id))/delivery")
    }

    static func disputeResolution(
        id: String,
        action: ReviewDisputeResolutionAction,
        bodyText: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/disputes/\(pathSegment(id))/resolution",
            body: ResolveReviewDisputeBody(action: action, bodyText: bodyText)
        )
    }

    static func disputeResolutionDrafts(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/disputes/\(pathSegment(id))/resolution-drafts")
    }
}

private extension PostClearanceAction {
    var staffReasonCode: String {
        switch self {
        case .approved: "staff_approved"
        case .rejected: "staff_rejected"
        case .inReview: "staff_reviewed"
        }
    }
}

import Foundation
import VouchaModels

private struct CommunityAutomodFeedbackBody: Encodable {
    let outcome: CommunityAutomodFeedbackOutcome
    let action: CommunityAutomodFeedbackAction
    let reasonCode: String?
    let note: String?
}

private struct CommunityAutomodSettingsBody: Encodable {
    let automodAction: CommunityAutomodActionSetting
}

public extension Endpoint {
    static func dismissCommunityAutomodFlag(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))/automod-flag/dismissal"
        )
    }

    static func updateCommunityAutomodSettings(
        idOrSlug: String,
        automodAction: CommunityAutomodActionSetting
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/automod-settings",
            body: CommunityAutomodSettingsBody(automodAction: automodAction)
        )
    }

    static func communityAutomodRecentActions(
        idOrSlug: String,
        after: String? = nil,
        limit: Int? = nil,
        window: String? = nil,
        source: String? = nil,
        agent: String? = nil,
        postType: String? = nil,
        maxConfidence: Double? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let window {
            items.append(.init(name: "window", value: window))
        }
        if let source {
            items.append(.init(name: "source", value: source))
        }
        if let agent {
            items.append(.init(name: "agent", value: agent))
        }
        if let postType {
            items.append(.init(name: "post_type", value: postType))
        }
        if let maxConfidence {
            items.append(.init(name: "max_confidence", value: "\(maxConfidence)"))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/automod/recent-actions",
            queryItems: items
        )
    }

    static func simulateCommunityAutomod(
        idOrSlug: String,
        body: CommunityAutomodSimulationBody
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/automod/simulate",
            body: body
        )
    }

    static func recordCommunityAutomodFeedback(
        idOrSlug: String,
        sourceKey: String,
        outcome: CommunityAutomodFeedbackOutcome,
        action: CommunityAutomodFeedbackAction,
        reasonCode: String? = nil,
        note: String? = nil
    ) -> Endpoint {
        let path = "/api/v1/communities/\(pathSegment(idOrSlug))"
        return Endpoint(
            .POST,
            path: "\(path)/automod/recent-actions/\(pathSegment(sourceKey))/feedback",
            body: CommunityAutomodFeedbackBody(outcome: outcome, action: action, reasonCode: reasonCode, note: note)
        )
    }
}

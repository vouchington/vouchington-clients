import Foundation

private struct CommunityModeratorVacationBody: Encodable {
    let endsAt: Date?
}

private struct VacationDigestPreferenceBody: Encodable {
    let suppressCommunityDigestsWhileOnVacation: Bool

    private enum CodingKeys: String, CodingKey {
        case suppressCommunityDigestsWhileOnVacation = "shouldSuppressCommunityDigestsWhileOnVacation"
    }
}

public extension Endpoint {
    static func communityModeratorVacation(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderator-vacation")
    }

    static func setCommunityModeratorVacation(idOrSlug: String, endsAt: Date?) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderator-vacation",
            body: CommunityModeratorVacationBody(endsAt: endsAt)
        )
    }

    static func clearCommunityModeratorVacation(idOrSlug: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderator-vacation")
    }

    static func setSuppressCommunityDigestsWhileOnVacation(idOrSlug: String, suppress: Bool) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderator-vacation",
            body: VacationDigestPreferenceBody(
                suppressCommunityDigestsWhileOnVacation: suppress
            )
        )
    }
}

import Foundation

public extension Endpoint {
    static func activeCommunityBans(
        idOrSlug: String,
        userId: String,
        after: String? = nil,
        limit: Int = 20
    ) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/bans/users/\(pathSegment(userId))",
            queryItems: [.init(name: "limit", value: String(limit))] + (after.map { [.init(
                name: "after",
                value: $0
            )] } ?? [])
        )
    }

    static func communityBan(idOrSlug: String, banId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/bans/\(pathSegment(banId))")
    }

    static func liftAllCommunityBans(idOrSlug: String, userId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/bans/users/\(pathSegment(userId))")
    }
}

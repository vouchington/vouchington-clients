import Foundation

public extension Endpoint {
    static var scopeCatalog: Endpoint {
        Endpoint(.GET, path: "/api/v1/scopes")
    }

    static func myOAuthGrants(after: String? = nil, limit: Int = 25) -> Endpoint {
        pagedGet(path: "/api/v1/my/oauth-grants", after: after, limit: limit)
    }

    static func revokeMyOAuthGrant(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/oauth-grants/\(pathSegment(id))")
    }
}

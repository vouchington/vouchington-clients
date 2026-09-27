import Foundation

public extension Endpoint {
    static var scopeCatalog: Endpoint {
        Endpoint(.GET, path: "/api/v1/scopes")
    }

    static func myOAuthGrants(after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let after { queryItems.append(URLQueryItem(name: "after", value: after)) }
        return Endpoint(.GET, path: "/api/v1/my/oauth-grants", queryItems: queryItems)
    }

    static func revokeMyOAuthGrant(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/oauth-grants/\(pathSegment(id))")
    }
}

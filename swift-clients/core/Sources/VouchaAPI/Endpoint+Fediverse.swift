import Foundation

public extension Endpoint {
    static func fediverseInstances(
        after: String? = nil,
        limit: Int? = 25,
        query: String? = nil,
        sort: String? = "best"
    ) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if let limit {
            queryItems.append(URLQueryItem(name: "limit", value: String(limit)))
        }
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        if let query {
            queryItems.append(URLQueryItem(name: "q", value: query))
        }
        if let sort {
            queryItems.append(URLQueryItem(name: "sort", value: sort))
        }
        return Endpoint(.GET, path: "/api/v1/fediverse/instances", queryItems: queryItems)
    }

    static func fediverseInstance(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/fediverse/instances/\(pathSegment(idOrSlug))")
    }
}

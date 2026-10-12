import Foundation

public extension Endpoint {
    static func fediverseInstances(
        after: String? = nil,
        limit: Int? = 25,
        query: String? = nil,
        sort: String? = "best"
    ) -> Endpoint {
        var filters: [URLQueryItem] = []
        if let query {
            filters.append(URLQueryItem(name: "q", value: query))
        }
        if let sort {
            filters.append(URLQueryItem(name: "sort", value: sort))
        }
        return pagedGet(
            path: "/api/v1/fediverse/instances",
            after: after,
            limit: limit,
            queryItems: filters
        )
    }

    static func fediverseInstance(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/fediverse/instances/\(pathSegment(idOrSlug))")
    }
}

import Foundation

public extension Endpoint {
    static func post(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/posts/\(pathSegment(idOrSlug))")
    }

    static func postDescendants(postId: String, after: String? = nil, limit: Int = 100) -> Endpoint {
        postCollection(postId, "descendants", after: after, limit: limit)
    }

    static func postAncestors(postId: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        postCollection(postId, "ancestors", after: after, limit: limit)
    }
}

private func postCollection(_ postId: String, _ collection: String, after: String?, limit: Int?) -> Endpoint {
    Endpoint.pagedGet(
        path: "/api/v1/posts/\(Endpoint.pathSegment(postId))/\(collection)",
        after: after,
        limit: limit
    )
}

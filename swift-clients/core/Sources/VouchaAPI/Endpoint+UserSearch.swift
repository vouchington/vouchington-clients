import Foundation

public extension Endpoint {
    /// GET `/api/v1/users` search. Query order is `q`, then `after` when a cursor is present, then `limit`.
    /// A nil cursor is omitted. Callers that place `after` after `limit` are a different query shape.
    static func searchUsers(query: String, after: String? = nil, limit: Int = 10) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/users",
            queryItems: [
                URLQueryItem(name: "q", value: query),
                URLQueryItem(name: "after", value: after),
                URLQueryItem(name: "limit", value: "\(limit)")
            ].compactMap { $0.value == nil ? nil : $0 }
        )
    }
}

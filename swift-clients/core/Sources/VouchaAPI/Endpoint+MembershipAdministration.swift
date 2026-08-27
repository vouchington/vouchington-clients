import Foundation

public extension Endpoint {
    static func membershipGrantUserSearch(query: String, after: String? = nil, limit: Int = 10) -> Endpoint {
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

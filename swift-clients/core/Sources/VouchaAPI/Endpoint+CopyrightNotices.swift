import Foundation

public extension Endpoint {
    static func copyrightNotices(after: String? = nil, limit: Int? = nil) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if let limit {
            queryItems.append(URLQueryItem(name: "limit", value: String(limit)))
        }
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/copyright-notices", queryItems: queryItems)
    }

    static func copyrightNotice(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/copyright-notices/\(pathSegment(id))")
    }

    static func copyrightParticipantNotice(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/copyright-notices/\(pathSegment(id))/participant")
    }

    static func copyrightEuDisputeSettlements(id: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if let limit {
            queryItems.append(URLQueryItem(name: "limit", value: String(limit)))
        }
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/copyright-notices/\(pathSegment(id))/eu-dispute-settlements",
            queryItems: queryItems
        )
    }
}

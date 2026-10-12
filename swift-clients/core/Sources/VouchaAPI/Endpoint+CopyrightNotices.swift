import Foundation

public extension Endpoint {
    static func copyrightNotices(after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(path: "/api/v1/copyright-notices", after: after, limit: limit)
    }

    static func copyrightNotice(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/copyright-notices/\(pathSegment(id))")
    }

    static func copyrightParticipantNotice(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/copyright-notices/\(pathSegment(id))/participant")
    }

    static func copyrightEuDisputeSettlements(id: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(
            path: "/api/v1/copyright-notices/\(pathSegment(id))/eu-dispute-settlements",
            after: after,
            limit: limit
        )
    }
}

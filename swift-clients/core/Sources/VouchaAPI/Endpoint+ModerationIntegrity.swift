import Foundation
import VouchaModels

private struct DismissReportIntegrityFlagBody: Encodable {
    let resolution = ReportIntegrityResolution.dismissed
}

private struct ResolveVoteIntegrityFlagBody: Encodable {
    let resolution: VoteIntegrityResolution
}

public extension Endpoint {
    static func reportIntegrityFlags(
        status: IntegrityFlagStatus? = nil,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let status {
            items.append(.init(name: "status", value: status.rawValue))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/report-integrity/flags", queryItems: items)
    }

    static func reportIntegrityFlag(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/report-integrity/flags/\(pathSegment(id))")
    }

    static func dismissReportIntegrityFlag(id: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/report-integrity/flags/\(pathSegment(id))",
            body: DismissReportIntegrityFlagBody()
        )
    }

    static func applyReportIntegrityPenalty(flagId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/report-integrity/flags/\(pathSegment(flagId))/penalties")
    }

    static func voteIntegrityFlags(
        status: IntegrityFlagStatus? = nil,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let status {
            items.append(.init(name: "status", value: status.rawValue))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/vote-integrity/flags", queryItems: items)
    }

    static func voteIntegrityFlag(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/vote-integrity/flags/\(pathSegment(id))")
    }

    static func resolveVoteIntegrityFlag(id: String, resolution: VoteIntegrityResolution) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/vote-integrity/flags/\(pathSegment(id))",
            body: ResolveVoteIntegrityFlagBody(resolution: resolution)
        )
    }

    static func applyVoteIntegrityPenalty(flagId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/vote-integrity/flags/\(pathSegment(flagId))/penalties")
    }

    static func reportIntegrityPenalties(
        status: IntegrityPenaltyStatus? = nil,
        userId: String? = nil,
        sourceFlagId: String? = nil,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/report-integrity/penalties",
            queryItems: integrityPenaltyQueryItems(
                status: status,
                userId: userId,
                sourceFlagId: sourceFlagId,
                after: after,
                limit: limit
            )
        )
    }

    static func reportIntegrityPenalty(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/report-integrity/penalties/\(pathSegment(id))")
    }

    static func revokeReportIntegrityPenalty(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/report-integrity/penalties/\(pathSegment(id))")
    }

    static func voteIntegrityPenalties(
        status: IntegrityPenaltyStatus? = nil,
        userId: String? = nil,
        sourceFlagId: String? = nil,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        var items = integrityPenaltyQueryItems(
            status: status,
            userId: userId,
            sourceFlagId: sourceFlagId,
            after: after,
            limit: limit
        )
        items.insert(.init(name: "source", value: "flag"), at: 0)
        return Endpoint(.GET, path: "/api/v1/vote-integrity/penalties", queryItems: items)
    }

    static func voteIntegrityPenalty(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/vote-integrity/penalties/\(pathSegment(id))")
    }

    static func revokeVoteIntegrityPenalty(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/vote-integrity/penalties/\(pathSegment(id))")
    }

    private static func integrityPenaltyQueryItems(
        status: IntegrityPenaltyStatus?,
        userId: String?,
        sourceFlagId: String?,
        after: String?,
        limit: Int?
    ) -> [URLQueryItem] {
        var items: [URLQueryItem] = []
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        if let status {
            items.append(.init(name: "status", value: status.rawValue))
        }
        if let userId {
            items.append(.init(name: "user_id", value: userId))
        }
        if let sourceFlagId {
            items.append(.init(name: "source_flag_id", value: sourceFlagId))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return items
    }
}

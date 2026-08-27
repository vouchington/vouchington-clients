import Foundation
import VouchaModels

private struct ReportResolutionBody: Encodable {
    let status: ModerationReportResolution
}

private struct AdminWarningBody: Encodable {
    let userId: String
    let reason: String
    let publicMessage: String?
    let reportId: String
    let resolveReport = true
}

public enum AdminWarningValidationError: Error, Equatable {
    case reasonTooLong
    case publicMessageTooLong
}

public enum AdminWarningLimits {
    public static let reasonUTF16 = 1_000
    public static let publicMessageUTF16 = 2_000
}

public extension Endpoint {
    static func moderationReports(
        status: ModerationReportStatus? = .pending,
        sort: ModerationReportSort? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        reports(status: status, limit: limit, after: after, sort: sort)
    }

    static func clusteredModerationReports(
        status: ModerationReportStatus? = .pending,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        reports(status: status, limit: limit, after: after, cluster: "entity")
    }

    static func reports(
        status: ModerationReportStatus? = nil,
        limit: Int = 25,
        after: String? = nil,
        sort: ModerationReportSort? = nil,
        cluster: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let status {
            items.append(.init(name: "status", value: status.rawValue))
        }
        if let sort {
            items.append(.init(name: "sort", value: sort.rawValue))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let cluster {
            items.append(.init(name: "cluster", value: cluster))
        }
        return Endpoint(.GET, path: "/api/v1/reports", queryItems: items)
    }

    static func resolveModerationReport(
        reportId: String,
        status: ModerationReportResolution
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/reports/\(pathSegment(reportId))",
            body: ReportResolutionBody(status: status)
        )
    }

    static func rerunModerationReportJudgement(reportId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/reports/\(pathSegment(reportId))/judgements")
    }

    static func issueAdminWarning(
        userId: String,
        reason: String,
        publicMessage: String? = nil,
        reportId: String
    ) throws -> Endpoint {
        guard reason.utf16.count <= AdminWarningLimits.reasonUTF16 else {
            throw AdminWarningValidationError.reasonTooLong
        }
        guard (publicMessage?.utf16.count ?? 0) <= AdminWarningLimits.publicMessageUTF16 else {
            throw AdminWarningValidationError.publicMessageTooLong
        }
        return Endpoint(
            .POST,
            path: "/api/v1/admin/warnings",
            body: AdminWarningBody(
                userId: userId,
                reason: reason,
                publicMessage: publicMessage,
                reportId: reportId
            ),
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func confirmCommunityBanEvasion(communityIdOrSlug: String, userId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(communityIdOrSlug))/ban-evasion/\(pathSegment(userId))"
        )
    }

    static func dismissCommunityBanEvasion(communityIdOrSlug: String, userId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/communities/\(pathSegment(communityIdOrSlug))/ban-evasion/\(pathSegment(userId))"
        )
    }
}

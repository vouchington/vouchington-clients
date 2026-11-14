import Foundation

public extension Endpoint {
    static func reportIntegrityPenaltiesReviewDue(after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(path: "/api/v1/report-integrity/penalties/review-due", after: after, limit: limit)
    }

    static func voteIntegrityPenaltiesReviewDue(after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(path: "/api/v1/vote-integrity/penalties/review-due", after: after, limit: limit)
    }

    static func reviewReportIntegrityPenalty(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/report-integrity/penalties/\(pathSegment(id))/reviews")
    }

    static func reviewVoteIntegrityPenalty(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/vote-integrity/penalties/\(pathSegment(id))/reviews")
    }

    static func copyrightRepeatInfringerAccounts(noticeId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/copyright-notices/\(pathSegment(noticeId))/repeat-infringer-accounts")
    }

    static func copyrightStaffReviewQueue(after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(path: "/api/v1/copyright-notices/review-queue", after: after, limit: limit)
    }

    static func copyrightNoticeAssociationTargets(query: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(
            path: "/api/v1/copyright-notices/association-targets", after: after, limit: limit,
            queryItems: [.init(name: "q", value: query)]
        )
    }
}

@testable import VouchaAPI
import VouchaModels

let reviewQueueInReviewPostId = "019e8300-5e60-7000-8000-000000000000"
let reviewQueueRejectedPostId = "019e82f2-a2c0-7000-8000-000000000000"
let reviewQueuePageOneEndCursor = "eyJpZCI6IjAxOWU4MmYyLWEyYzAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMCJ9"
let moderationFlatReportId = "019f6559-6b31-7171-b5b1-ccd9d702c45e"
let moderationClusteredPageOneEndCursor =
    "eyJjbHVzdGVyIjp0cnVlLCJjcmVhdGVkX2F0IjoiMjAyNi0wNi0wMVQxMjowNTowMC4wMDAwMDBaIiwiZW50aXR5X3R5cGUiOiJwb3N0IiwiaWQiOiIwMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDAxMDMiLCJzb3J0IjoiY3JlYXRlZF9hdF9kZXNjIiwic3RhdHVzIjoicGVuZGluZyIsInNjb3BlIjoie1wiYXVkaWVuY2VcIjpcInN0YWZmXCIsXCJvd25lcklkXCI6bnVsbH0ifQ"

extension ApiFixtureEndpointCoverageTests {
    static let moderationEndpointRegistry: [String: Endpoint] = [
        "native.moderation.reports.default": Endpoint.moderationReports(status: .pending, limit: 25),
        "native.moderation.reports.clustered.default": Endpoint.clusteredModerationReports(
            status: .pending,
            limit: 4
        ),
        "native.moderation.reports.member.default": Endpoint.moderationReports(status: .pending, limit: 25),
        "native.moderation.reports.clustered.page-2": Endpoint.clusteredModerationReports(
            status: .pending,
            after: moderationClusteredPageOneEndCursor,
            limit: 3
        ),
        "native.moderation.report-judgement.default": Endpoint.rerunModerationReportJudgement(
            reportId: moderationFlatReportId
        ),
        "native.moderation.report-resolution.reviewed": Endpoint.resolveModerationReport(
            reportId: moderationFlatReportId,
            status: .reviewed
        ),
        "native.moderation.admin-warning.report": adminWarningFixtureEndpoint(),
        "native.moderation.ban-evasion.confirm": confirmBanEvasionFixtureEndpoint(),
        "native.moderation.ban-evasion.dismiss": dismissBanEvasionFixtureEndpoint(),
        "native.moderation.appeals.default": Endpoint.appeals(status: .pending, limit: 25),
        "native.moderation.disputes.default": Endpoint.disputes(status: .pending, limit: 25),
        "native.moderation.review-queue.default": Endpoint.adminReviewQueue(limit: 2),
        "native.moderation.review-queue.page-2": Endpoint.adminReviewQueue(
            after: reviewQueuePageOneEndCursor,
            limit: 2
        ),
        "native.moderation.clearance.approved": Endpoint.updatePostClearance(
            postId: reviewQueueInReviewPostId,
            status: .approved
        ),
        "native.moderation.clearance.rejected": Endpoint.updatePostClearance(
            postId: reviewQueueInReviewPostId,
            status: .rejected
        ),
        "native.moderation.clearance.in-review": Endpoint.updatePostClearance(
            postId: reviewQueueRejectedPostId,
            status: .inReview
        ),
        "native.moderation.modlog.default": Endpoint.adminModlog(),
        "native.moderation.analytics.default": Endpoint.adminModerationAnalytics(range: "30d"),
        "native.moderation.transparency.default": Endpoint.moderationTransparency(range: "30d"),
        "native.community.moderation-transparency.default": Endpoint.communityModerationTransparency(
            idOrSlug: "fixture-community",
            range: "30d"
        ),
        "native.moderation.vote-integrity.default": Endpoint.voteIntegrityFlags(),
        "native.moderation.report-integrity.default": Endpoint.reportIntegrityFlags()
    ]

    private static func adminWarningFixtureEndpoint() -> Endpoint {
        try! Endpoint.issueAdminWarning(
            userId: "user-2",
            reason: "Repeated harassment",
            publicMessage: "Stop contacting this user.",
            reportId: moderationFlatReportId
        )
    }

    private static func confirmBanEvasionFixtureEndpoint() -> Endpoint {
        Endpoint.confirmCommunityBanEvasion(communityIdOrSlug: "community-1", userId: "user-2")
    }

    private static func dismissBanEvasionFixtureEndpoint() -> Endpoint {
        Endpoint.dismissCommunityBanEvasion(communityIdOrSlug: "community-1", userId: "user-2")
    }
}

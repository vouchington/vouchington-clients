@testable import VouchaAPI

let contractDriftFixtureEndpoints: [String: Endpoint] = [
    "native.moderation.report-integrity.flag": .reportIntegrityFlag(id: "report-flag-1"),
    "native.moderation.report-integrity.penalties.review-due": .reportIntegrityPenaltiesReviewDue(),
    "native.moderation.report-integrity.penalties.review": .reviewReportIntegrityPenalty(
        id: "019f7000-0000-7000-8000-000000000101"
    ),
    "native.moderation.vote-integrity.penalties.review-due": .voteIntegrityPenaltiesReviewDue(),
    "native.moderation.vote-integrity.penalties.review": .reviewVoteIntegrityPenalty(
        id: "019f7000-0000-7000-8000-000000000201"
    ),
    "native.moderation.report-integrity.penalties.review-due.page-1": .reportIntegrityPenaltiesReviewDue(limit: 1),
    "native.moderation.report-integrity.penalties.review-due.page-2": .reportIntegrityPenaltiesReviewDue(
        after: reportReviewCursor,
        limit: 1
    ),
    "native.moderation.vote-integrity.penalties.review-due.page-1": .voteIntegrityPenaltiesReviewDue(limit: 1),
    "native.moderation.vote-integrity.penalties.review-due.page-2": .voteIntegrityPenaltiesReviewDue(
        after: voteReviewCursor,
        limit: 1
    ),
    "native.copyright.repeat-infringer-accounts": .copyrightRepeatInfringerAccounts(
        noticeId: "00000000-0000-7000-8000-000000000804"
    ),
    "web.copyright.staff-queue.default": .copyrightStaffReviewQueue(),
    "web.copyright.email-notice-association-search": .copyrightNoticeAssociationTargets(query: "Claimant"),
    "web.admin.psql.jobs.classifier-range": .psqlJobs(
        type: .createPartitions,
        classifierRange: .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: 100)
    )
]

private let reportReviewCursor =
    "eyJ0aW1lc3RhbXAiOiIyMDI2LTEwLTAxVDEyOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAxOWY3MDAwLTAw"
        + "MDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMSIsInNjb3BlIjoicmVwb3J0LWludGVncml0eTpwZW5hbHR5"
        + "LXJldmlld3M6ZHVlOmFzYyJ9"

private let voteReviewCursor =
    "eyJ0aW1lc3RhbXAiOiIyMDI2LTEwLTAxVDEyOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAxOWY3MDAwLTAw"
        + "MDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoidm90ZS1pbnRlZ3JpdHk6cGVuYWx0eS1y"
        + "ZXZpZXdzOmR1ZTphc2MifQ"

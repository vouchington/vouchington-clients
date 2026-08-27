@testable import VouchaAPI

let moderationIntegrityEndpointFixtureCoverage: [String: Endpoint] = [
    "native.moderation.vote-integrity.default": Endpoint.voteIntegrityFlags(),
    "native.moderation.vote-integrity.pending": Endpoint.voteIntegrityFlags(status: .pending),
    "native.moderation.vote-integrity.resolved": Endpoint.voteIntegrityFlags(status: .resolved),
    "native.moderation.vote-integrity.resolution.dismissed": Endpoint.resolveVoteIntegrityFlag(
        id: "vote-flag-1",
        resolution: .dismissed
    ),
    "native.moderation.vote-integrity.resolution.penalized": Endpoint.resolveVoteIntegrityFlag(
        id: "vote-flag-1",
        resolution: .penalized
    ),
    "native.moderation.vote-integrity.resolution.suspended": Endpoint.resolveVoteIntegrityFlag(
        id: "vote-flag-1",
        resolution: .suspended
    ),
    "native.moderation.vote-integrity.penalty": Endpoint.applyVoteIntegrityPenalty(flagId: "vote-flag-1"),
    "native.moderation.report-integrity.default": Endpoint.reportIntegrityFlags(),
    "native.moderation.report-integrity.pending": Endpoint.reportIntegrityFlags(status: .pending),
    "native.moderation.report-integrity.resolved": Endpoint.reportIntegrityFlags(status: .resolved),
    "native.moderation.report-integrity.resolution.dismissed": Endpoint.dismissReportIntegrityFlag(
        id: "report-flag-1"
    ),
    "native.moderation.report-integrity.penalty": Endpoint.applyReportIntegrityPenalty(flagId: "report-flag-1"),
    "native.moderation.report-integrity.penalties.default": Endpoint.reportIntegrityPenalties(),
    "native.moderation.report-integrity.penalties.active": Endpoint.reportIntegrityPenalties(status: .active),
    "native.moderation.report-integrity.penalties.revoked": Endpoint.reportIntegrityPenalties(status: .revoked),
    "native.moderation.report-integrity.penalties.all": Endpoint.reportIntegrityPenalties(),
    "native.moderation.report-integrity.penalties.page-2": Endpoint.reportIntegrityPenalties(
        status: .active,
        after: reportPenaltyPageCursor
    ),
    "native.moderation.report-integrity.penalties.get": Endpoint.reportIntegrityPenalty(
        id: "019f7000-0000-7000-8000-000000000101"
    ),
    "native.moderation.report-integrity.penalties.revoke": Endpoint.revokeReportIntegrityPenalty(
        id: "019f7000-0000-7000-8000-000000000101"
    ),
    "native.moderation.vote-integrity.penalties.default": Endpoint.voteIntegrityPenalties(),
    "native.moderation.vote-integrity.penalties.active": Endpoint.voteIntegrityPenalties(status: .active),
    "native.moderation.vote-integrity.penalties.revoked": Endpoint.voteIntegrityPenalties(status: .revoked),
    "native.moderation.vote-integrity.penalties.all": Endpoint.voteIntegrityPenalties(),
    "native.moderation.vote-integrity.penalties.page-2": Endpoint.voteIntegrityPenalties(
        status: .active,
        after: votePenaltyPageCursor
    ),
    "native.moderation.vote-integrity.penalties.get": Endpoint.voteIntegrityPenalty(
        id: "019f7000-0000-7000-8000-000000000201"
    ),
    "native.moderation.vote-integrity.penalties.revoke": Endpoint.revokeVoteIntegrityPenalty(
        id: "019f7000-0000-7000-8000-000000000201"
    )
]

let reportPenaltyPageCursor = [
    "eyJpZCI6IjAxOWY3MDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiIsInNjb3BlIjoie1wicmVzb3VyY2VcIjpcIn",
    "JlcG9ydC1hYnVzZS1wZW5hbHRpZXNcIixcInN0YXR1c1wiOlwiYWN0aXZlXCIsXCJ1c2VyX2lkXCI6bnVsbCxcInNvdXJj",
    "ZV9mbGFnX2lkXCI6bnVsbCxcIm9yZGVyXCI6XCJpZC1kZXNjXCJ9In0"
].joined()

let votePenaltyPageCursor = [
    "eyJpZCI6IjAxOWY3MDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMyIsInNjb3BlIjoie1wicmVzb3VyY2VcIjpcIn",
    "ZvdGUtd2VpZ2h0LXBlbmFsdGllc1wiLFwic3RhdHVzXCI6XCJhY3RpdmVcIixcInNvdXJjZVwiOlwiZmxhZ1wiLFwidXNl",
    "cl9pZFwiOm51bGwsXCJzb3VyY2VfZmxhZ19pZFwiOm51bGwsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9"
].joined()

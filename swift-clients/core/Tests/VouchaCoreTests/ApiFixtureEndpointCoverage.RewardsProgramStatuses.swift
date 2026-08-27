import Foundation
@testable import VouchaAPI
import VouchaModels

extension ApiFixtureEndpointCoverageTests {
    static let rewardsProgramStatusFixtureEndpoints: [String: Endpoint] = [
        "native.rewards-program-status-topics.search.default": .rewardsProgramStatusTopics(query: "Gold"),
        "native.rewards-statuses.empty": .rewardsProgramStatuses(),
        "native.rewards-statuses.page-1": .rewardsProgramStatuses(limit: 2),
        "native.rewards-statuses.page-2": .rewardsProgramStatuses(
            after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MiIsInNjb3BlIjoibXktcmV3YXJkcy1wcm9ncmFtLXN0YXR1c2VzOjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MDppZC1hc2MifQ",
            limit: 2
        ),
        "native.rewards-statuses.create.default": .createRewardsProgramStatus(
            body: .init(rewardsProgramStatusId: "00000000-0000-7000-8000-000000000751")
        ),
        "native.rewards-statuses.update.full": .updateRewardsProgramStatus(
            id: "00000000-0000-7000-8000-000000000741",
            body: .init(
                since: .value(LocalDate("2025-02-01")!),
                until: .value(LocalDate("2026-12-31")!)
            )
        ),
        "native.rewards-statuses.update.clear-dates": .updateRewardsProgramStatus(
            id: "00000000-0000-7000-8000-000000000741",
            body: .init(since: .null, until: .null)
        ),
        "native.rewards-statuses.delete.default": .deleteRewardsProgramStatus(
            id: "00000000-0000-7000-8000-000000000741"
        )
    ]
}

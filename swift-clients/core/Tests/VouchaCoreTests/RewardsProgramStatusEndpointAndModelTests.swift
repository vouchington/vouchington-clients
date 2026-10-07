import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class RewardsProgramStatusEndpointAndModelTests: XCTestCase {
    func testDecodesDatesAndLegacyPage() throws {
        let page = try makeVouchaDecoder().decode(RewardsProgramStatusPage.self, from: json(pageInfo: ""))
        let status = try XCTUnwrap(page.results.first)
        XCTAssertEqual(status.id, "entry-1")
        XCTAssertEqual(status.rewardsProgramStatusId, "topic-1")
        XCTAssertEqual(status.since?.description, "2026-01-02")
        XCTAssertNil(status.until)
        XCTAssertFalse(page.pageInfo.hasNextPage)
    }

    func testEndpointsUseOpaqueCursorAndNullableDates() throws {
        XCTAssertEqual(Endpoint.rewardsProgramStatuses(after: "opaque", limit: 2).queryItems, [
            URLQueryItem(name: "limit", value: "2"), URLQueryItem(name: "after", value: "opaque")
        ])
        assertEndpoint(
            .createRewardsProgramStatus(body: .init(rewardsProgramStatusId: "topic-1")),
            method: .POST,
            path: "/api/v1/my/rewards-program-statuses",
            body: ["rewards_program_status_topic_id": "topic-1"]
        )
        try assertEndpoint(
            .updateRewardsProgramStatus(
                id: "entry/1",
                body: .init(since: .value(XCTUnwrap(LocalDate("2026-01-02"))), until: .null)
            ),
            method: .PATCH,
            path: "/api/v1/my/rewards-program-statuses/entry%2F1",
            body: ["started_on": "2026-01-02", "expires_on": NSNull()]
        )
        XCTAssertEqual(Endpoint.rewardsProgramStatusTopics(query: "Gold").queryItems, [
            URLQueryItem(name: "q", value: "Gold"),
            URLQueryItem(name: "topic_types", value: "rewards_program_status"),
            URLQueryItem(name: "limit", value: "10")
        ])
    }

    private func json(pageInfo: String) -> Data {
        Data(
            (
                "{\"results\":[{\"id\":\"entry-1\",\"rewards_program_status_topic_id\":\"topic-1\",\"started_on\":\"2026-01-02\",\"expires_on\":null,\"rewards_program_status\":{\"id\":\"topic-1\",\"name\":\"Gold\",\"slug\":\"gold\"}}]" +
                    pageInfo + "}"
            ).utf8
        )
    }
}

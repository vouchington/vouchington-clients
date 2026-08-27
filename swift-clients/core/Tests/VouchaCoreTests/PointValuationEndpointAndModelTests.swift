import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class PointValuationEndpointAndModelTests: XCTestCase {
    func testDecodesPaginatedPointValuationFixtures() throws {
        let paginated = try makeVouchaDecoder().decode(
            PointValuationPage.self,
            from: ApiFixtureLoader.data("native.point-valuations.page-1")
        )

        XCTAssertEqual(
            paginated.results.first?.valuePerPoint,
            try ScaledMoney(amount: 35_000, currency: "usd")
        )
        XCTAssertEqual(paginated.results.first?.rewardsProgram.name, "Travel Rewards")
        XCTAssertTrue(paginated.pageInfo.hasNextPage)
    }

    func testPointValuationEndpointsEncodeScaledMoneyBodiesAndCursor() throws {
        let page = Endpoint.pointValuations(after: "opaque+/cursor", limit: 2)
        XCTAssertEqual(page.queryItems, [
            URLQueryItem(name: "limit", value: "2"),
            URLQueryItem(name: "after", value: "opaque+/cursor")
        ])
        try assertEndpoint(
            .createPointValuation(body: .init(
                rewardsProgramId: "program-1",
                valuePerPoint: ScaledMoney(amount: 35_000, currency: "usd")
            )),
            method: .POST,
            path: "/api/v1/my/rewards-program-point-valuations",
            body: [
                "rewards_program_id": "program-1",
                "value_per_point": ["amount": 35_000, "currency": "usd", "scale": 6]
            ]
        )
        try assertEndpoint(
            .updatePointValuation(
                id: "valuation/one",
                body: .init(
                    valuePerPoint: ScaledMoney(amount: 0, currency: "usd"),
                    note: .null
                )
            ),
            method: .PATCH,
            path: "/api/v1/my/rewards-program-point-valuations/valuation%2Fone",
            body: [
                "value_per_point": ["amount": 0, "currency": "usd", "scale": 6],
                "note": NSNull()
            ]
        )
        assertEndpoint(
            .deletePointValuation(id: "valuation/one"),
            method: .DELETE,
            path: "/api/v1/my/rewards-program-point-valuations/valuation%2Fone"
        )
    }

    func testRewardsProgramSearchUsesOnlyRewardsPrograms() {
        let endpoint = Endpoint.rewardsProgramTopics(query: "Example")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "q", value: "Example"),
            URLQueryItem(name: "topic_types", value: "rewards_program"),
            URLQueryItem(name: "limit", value: "10")
        ])
    }
}

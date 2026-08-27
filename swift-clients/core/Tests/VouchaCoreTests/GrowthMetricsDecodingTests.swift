@testable import VouchaAPI
import VouchaModels
import XCTest

final class GrowthMetricsDecodingTests: XCTestCase {
    func testDecodesSharedGrowthMetricsFixture() throws {
        let metrics = try JSONDecoder.vouchaFixtureDecoder.decode(
            GrowthMetrics.self,
            from: ApiFixtureLoader.data("web.growth-metrics.default")
        )

        XCTAssertEqual(metrics.range, .thirtyDays)
        XCTAssertEqual(metrics.userGrowth.totalUsers, 1_200)
        XCTAssertEqual(metrics.userGrowth.signupsOverTime.first?.date, "2026-07-01")
        XCTAssertEqual(metrics.contentProduction.postsByType.dataPoint, 80)
        XCTAssertEqual(metrics.engagement.votesCast, 2_100)
        XCTAssertEqual(metrics.networkEffects.referralCoefficient, 2.4)
        XCTAssertEqual(metrics.revenue.membershipsByTier["plus"], 60)
        XCTAssertEqual(
            metrics.revenue.mrrByCurrency.first,
            try ScaledMoneyAggregate(amount: "2850000000", currency: "usd")
        )
        XCTAssertEqual(metrics.infrastructure.queueThroughput, 14_200)
    }

    func testGrowthMetricsEndpointUsesRangeQuery() {
        let endpoint = Endpoint.growthMetrics(range: .sevenDays)

        XCTAssertEqual(endpoint.method, .GET)
        XCTAssertEqual(endpoint.path, "/api/v1/growth-metrics")
        XCTAssertEqual(endpoint.queryItems.first?.name, "range")
        XCTAssertEqual(endpoint.queryItems.first?.value, "7d")
    }
}

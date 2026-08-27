import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class GrowthDashboardSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testLoadedDashboardRendersMetricSectionsAndValues() throws {
        let metrics = try fixtureMetrics()
        let sut = GrowthDashboardSurface(client: nil, initialMetrics: metrics)
        let inspection = try sut.inspect()

        XCTAssertEqual(try inspection.find(text: "Users").string(), "Users")
        XCTAssertEqual(try inspection.find(text: "Total users").string(), "Total users")
        XCTAssertEqual(try inspection.find(text: "1,200").string(), "1,200")
        XCTAssertEqual(try inspection.find(text: "Daily signups").string(), "Daily signups")
        XCTAssertEqual(try inspection.find(text: "Content").string(), "Content")
        XCTAssertEqual(try inspection.find(text: "Data points").string(), "Data points")
        XCTAssertEqual(try inspection.find(text: "Engagement").string(), "Engagement")
        XCTAssertEqual(try inspection.find(text: "Network").string(), "Network")
        XCTAssertEqual(try inspection.find(text: "Revenue").string(), "Revenue")
        XCTAssertEqual(try inspection.find(text: "Infrastructure").string(), "Infrastructure")
    }

    func testDashboardRendersEmptyStateBeforeLoading() throws {
        let sut = GrowthDashboardSurface(client: nil)

        XCTAssertEqual(
            try sut.inspect().find(text: "Growth metrics unavailable").string(),
            "Growth metrics unavailable"
        )
    }

    func testMrrFormattingPreservesScaleAndTrimsTrailingZeros() throws {
        let sut = GrowthDashboardSurface(client: nil)
        let micro = try ScaledMoneyAggregate(amount: "35000", currency: "usd")
        let whole = try ScaledMoneyAggregate(amount: "2850000000", currency: "usd")
        let aggregate = try ScaledMoneyAggregate(amount: "180143985094819820000", currency: "usd")

        XCTAssertEqual(UiMessages.string(sut.money(micro), locale: .english), "US$ 0.035")
        XCTAssertEqual(UiMessages.string(sut.money(whole), locale: .english), "US$ 2,850")
        XCTAssertEqual(
            UiMessages.string(sut.money(aggregate), locale: .english),
            "US$ 180,143,985,094,819.82"
        )
        XCTAssertEqual(
            UiMessages.string(sut.money(micro, locale: Locale(identifier: "pt_BR")), locale: .english),
            "US$ 0,035"
        )
        XCTAssertEqual(UiMessages.string(sut.moneyList([]), locale: .english), "0")
    }

    func testNativeRouteDestinationRendersGrowthDashboardWithRangeQuery() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/growth?range=90d"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(text: "Growth metrics unavailable"))
    }

    private func fixtureMetrics() throws -> GrowthMetrics {
        try JSONDecoder.vouchaFixtureDecoder.decode(
            GrowthMetrics.self,
            from: ApiFixtureLoader.data("web.growth-metrics.default")
        )
    }
}

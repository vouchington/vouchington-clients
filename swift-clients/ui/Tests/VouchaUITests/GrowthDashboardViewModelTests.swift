import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class GrowthDashboardViewModelTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.queuedHandlers = [:]
    }

    func testLoadFetchesDefaultGrowthMetricsFixture() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/growth-metrics"] = (
            ApiFixtureLoader.data("web.growth-metrics.default"), 200
        )
        let viewModel = try GrowthDashboardViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.metrics?.range, .thirtyDays)
        XCTAssertEqual(viewModel.metrics?.userGrowth.totalUsers, 1_200)
        XCTAssertEqual(viewModel.metrics?.engagement.votesCast, 2_100)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/growth-metrics")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "range=30d")
    }

    func testSelectRangeReloadsWithSelectedRange() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/growth-metrics"] = (
            ApiFixtureLoader.data("web.growth-metrics.default"), 200
        )
        let viewModel = try GrowthDashboardViewModel(client: makeClient())

        await viewModel.selectRange(.sevenDays)

        XCTAssertEqual(viewModel.range, .sevenDays)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "range=7d")
    }

    func testStaleRangeResponseDoesNotOverwriteCurrentMetrics() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/growth-metrics"] = [
            (ApiFixtureLoader.data("web.growth-metrics.default"), 200, 0.1),
            (growthMetricsFixture(range: "7d", totalUsers: 700), 200, 0)
        ]
        let viewModel = try GrowthDashboardViewModel(client: makeClient())

        let firstLoad = Task { await viewModel.load() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectRange(.sevenDays)
        await firstLoad.value

        XCTAssertEqual(viewModel.range, .sevenDays)
        XCTAssertEqual(viewModel.metrics?.range, .sevenDays)
        XCTAssertEqual(viewModel.metrics?.userGrowth.totalUsers, 700)
    }

    func testMissingClientSurfacesErrorState() async {
        let viewModel = GrowthDashboardViewModel(client: nil)

        await viewModel.load()

        if case .error = viewModel.state {} else {
            XCTFail("Expected missing API client to surface an error state")
        }
    }

    func testMalformedMetricsResponseSurfacesGenericErrorState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/growth-metrics"] = (
            Data("{\"range\":\"30d\"}".utf8), 200
        )
        let viewModel = try GrowthDashboardViewModel(client: makeClient())

        await viewModel.load()

        if case let .error(error) = viewModel.state {
            XCTAssertTrue(error.localizedDescription.contains("Failed to decode response"))
        } else {
            XCTFail("Expected malformed metrics response to surface an error state")
        }
    }

    private func growthMetricsFixture(range: String, totalUsers: Int) -> Data {
        let body = String(decoding: ApiFixtureLoader.data("web.growth-metrics.default"), as: UTF8.self)
            .replacingOccurrences(of: "\"range\": \"30d\"", with: "\"range\": \"\(range)\"")
            .replacingOccurrences(of: "\"total_users\": 1200", with: "\"total_users\": \(totalUsers)")
        return Data(body.utf8)
    }
}

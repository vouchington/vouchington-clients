import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteImportExportAndReportsTests: NativeRouteSurfaceViewModelTestCase {
    func testReportsRouteLoadsReportsEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (
            Data(#"{"results":[{"id":"report-1"}]}"#.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/reports")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationCases),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/reports")
        XCTAssertEqual(viewModel.rows.first?.title, "Reports")
        XCTAssertEqual(viewModel.rows.first?.detail, "1 item")
    }

    func testSourceImportExportRoutesUseDedicatedDestination() throws {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/news-sources/import-export")?.match)
        XCTAssertEqual(match.path, "/my/news-sources/import-export")
        XCTAssertEqual(try entry(for: .sourceImportExport).destinationIdentifier, .sourceImportExport)
    }

    func testTopicImportExportRouteUsesDedicatedDestination() throws {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/topics/import-export")?.match)
        XCTAssertEqual(match.path, "/my/topics/import-export")
        XCTAssertEqual(try entry(for: .topicImportExport).destinationIdentifier, .topicImportExport)
    }
}

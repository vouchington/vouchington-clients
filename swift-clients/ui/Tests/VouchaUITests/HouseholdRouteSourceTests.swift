import Foundation
import XCTest

final class HouseholdRouteSourceTests: XCTestCase {
    func testAppRoutingSourceMapsHouseholdToSettingsAndRestoresQueuedExactURL() throws {
        let mapping = try repoSource("swift-clients/apps/shared-app/NativeRouteDestinationIdentifier+AppRouting.swift")
        let routing = try repoSource("swift-clients/apps/shared-app/RootView+Routing.swift")
        let routeSelection = try repoSource("swift-clients/apps/shared-app/RootView+RouteSelection.swift")

        XCTAssertTrue(mapping
            .contains(".accountSettings, .profileSettings, .household, .paymentCards, .pointValuations,"))
        XCTAssertTrue(routing.contains("pendingNativeRouteURL = url"))
        XCTAssertTrue(routing.contains("routeNativeURL(pendingNativeRouteURL)"))
        XCTAssertTrue(routing.contains("""
                applyNativeRouteSelection(
                    entry: route.entry,
                    match: route.match,
        """))
        XCTAssertTrue(routeSelection.contains("selectedNativeRouteMatch = match"))
    }

    func testHouseholdRemovalSourceRequiresExplicitConfirmationAndCancel() throws {
        let source = try repoSource(
            "swift-clients/ui/Sources/VouchaFeatures/Households/HouseholdMembershipSectionView.swift"
        )

        XCTAssertTrue(source.contains(".nativeSwiftHouseholdsBookmarksRemoveMemberConfirmation"))
        XCTAssertTrue(source.contains(".nativeSwiftHouseholdsBookmarksRemoveMember"))
        XCTAssertTrue(source.contains(".nativeSwiftCommonCancel"))
        XCTAssertTrue(source.contains("role: .destructive"))
        XCTAssertTrue(source.contains("role: .cancel"))
        XCTAssertTrue(source.contains("pendingRemoval = nil"))
    }

    private func repoSource(_ relative: String, filePath: StaticString = #filePath) throws -> String {
        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        var roots = [current, URL(fileURLWithPath: "\(filePath)").deletingLastPathComponent()]
        for _ in 0 ..< 16 {
            roots.append(roots[roots.count - 1].deletingLastPathComponent())
        }
        for root in roots {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
        }
        throw XCTSkip("Could not find \(relative)")
    }
}

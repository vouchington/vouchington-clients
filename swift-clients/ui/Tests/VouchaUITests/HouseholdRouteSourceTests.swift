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
}

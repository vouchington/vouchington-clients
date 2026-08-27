import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class MembershipGrantRoutePresentationTests: NativeRouteSurfaceViewModelTestCase {
    func testMembershipGrantRouteUsesDedicatedSurfaceOutsideScrollView() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/memberships/grants"))
        let allowed = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: true
        )

        XCTAssertNoThrow(try allowed.inspect().find(text: "Membership grants"))
        XCTAssertNoThrow(try allowed.inspect().find(MembershipGrantSurface.self))
        XCTAssertThrowsError(try allowed.inspect().find(NativeListSurface.self))
        XCTAssertThrowsError(try allowed.inspect().find(ViewType.ScrollView.self))
    }
}

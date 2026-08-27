import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class IdentityVerificationAttemptGrantRoutePresentationTests: NativeRouteSurfaceViewModelTestCase {
    func testUserAdminRouteUsesDedicatedGrantSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/admin"))
        let allowed = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: true
        )

        XCTAssertEqual(route.entry.destinationIdentifier, .userAdmin)
        XCTAssertNoThrow(try allowed.inspect().find(IdentityVerificationAttemptGrantSurface.self))
        XCTAssertThrowsError(try allowed.inspect().find(NativeListSurface.self))
        XCTAssertThrowsError(try allowed.inspect().find(ViewType.ScrollView.self))
    }
}

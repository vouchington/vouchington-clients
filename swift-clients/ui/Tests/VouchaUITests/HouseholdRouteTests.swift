import Foundation
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class HouseholdRouteTests: XCTestCase {
    func testHouseholdHasExactDedicatedAuthenticatedRoute() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/household"))

        XCTAssertEqual(route.entry.destinationIdentifier, .household)
        XCTAssertEqual(route.match.template, "/my/household")
        XCTAssertEqual(route.match.path, "/my/household")
        XCTAssertEqual(route.entry.representativePath, "/my/household")
        XCTAssertFalse(NativeRouteDestinationIdentifier.household.supportsRemoteNativeSurface)
    }

    func testHouseholdNoLongerResolvesThroughGenericProfileSettings() throws {
        let profile = try XCTUnwrap(
            NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == .profileSettings }
        )

        XCTAssertFalse(profile.patterns.contains { $0.template == "/my/household" })
        XCTAssertEqual(NativeRouteParityChecker().destinationIdentifier(for: "/my/household"), .household)
    }

    func testHouseholdRouteKeepsSignedOutUsersAtAuthGate() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/household"))
        let sut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false,
            currentUserId: nil
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Create household"))
    }

    func testHouseholdRouteIdentityChangesWithAuthenticatedUser() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/household"))
        let first = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            currentUserId: "user-a"
        )
        let second = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            currentUserId: "user-b"
        )

        XCTAssertNotEqual(first.routeIdentity, second.routeIdentity)
    }

}

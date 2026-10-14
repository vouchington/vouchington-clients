import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class IntegrityAuthResetTests: NativeRouteSurfaceViewModelTestCase {
    func testGenericRouteReloadIdentityTracksAdministratorToModeratorSwitch() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let client = try makeClient()
        let administrator = NativeRouteDestinationSurface(
            entry: route.entry,
            client: client,
            routeMatch: route.match,
            routeQuery: nil,
            isSignedIn: true,
            currentUserId: "staff-1",
            isAdministrator: true,
            showSignIn: {}
        )
        let moderator = NativeRouteDestinationSurface(
            entry: route.entry,
            client: client,
            routeMatch: route.match,
            routeQuery: nil,
            isSignedIn: true,
            currentUserId: "staff-1",
            isSiteModerator: true,
            showSignIn: {}
        )

        XCTAssertTrue(administrator.shouldLoadRouteSurfaceContent)
        XCTAssertTrue(moderator.shouldLoadRouteSurfaceContent)
        XCTAssertTrue(administrator.viewModel.isStaffViewer)
        XCTAssertTrue(moderator.viewModel.isStaffViewer)
        XCTAssertNotEqual(administrator.authLoadTaskId, moderator.authLoadTaskId)
    }

    func testAuthDowngradeChangesIdentityAndRenderedSurfaceDropsAdminActions() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/vote-integrity/flags"))
        let client = try makeClient()
        let administrator = NativeRouteDestinationView(
            entry: route.entry,
            client: client,
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: true
        )
        let signedOut = NativeRouteDestinationView(
            entry: route.entry,
            client: client,
            routeMatch: route.match,
            isSignedIn: false,
            isAdministrator: false
        )

        XCTAssertNotEqual(administrator.routeIdentity, signedOut.routeIdentity)
        XCTAssertNoThrow(try administrator.inspect().find(text: "Pending"))
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try signedOut.inspect().find(button: "Apply vote penalty"))
        XCTAssertNil(NativeRouteDestinationSurface.resolvedClient(
            entry: route.entry,
            client: client,
            isSignedIn: false
        ))
    }

    func testClientReplacementChangesRouteIdentity() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/report-integrity/flags"))
        let first = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: true
        )
        let second = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: true
        )

        XCTAssertNotEqual(first.routeIdentity, second.routeIdentity)
    }

    func testReviewDisputesIdentityTracksAccountAndStaffRoleChanges() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/disputes"))
        let client = try makeClient()
        func surface(
            userId: String?,
            isSignedIn: Bool = true,
            isAdministrator: Bool = false,
            isSiteModerator: Bool = false
        ) -> NativeRouteDestinationSurface {
            NativeRouteDestinationSurface(
                entry: route.entry,
                client: client,
                routeMatch: route.match,
                routeQuery: nil,
                isSignedIn: isSignedIn,
                currentUserId: userId,
                isAdministrator: isAdministrator,
                isSiteModerator: isSiteModerator,
                showSignIn: {}
            )
        }

        let administrator = surface(userId: "admin-1", isAdministrator: true)
        let otherAdministrator = surface(userId: "admin-2", isAdministrator: true)
        let siteModerator = surface(userId: "admin-2", isSiteModerator: true)
        let member = surface(userId: "admin-2")
        let signedOut = surface(userId: nil, isSignedIn: false)
        let replacementClient = try makeClient()
        let replacementAdministrator = NativeRouteDestinationSurface(
            entry: route.entry,
            client: replacementClient,
            routeMatch: route.match,
            routeQuery: nil,
            isSignedIn: true,
            currentUserId: "admin-1",
            isAdministrator: true,
            showSignIn: {}
        )

        XCTAssertNotEqual(
            administrator.reviewDisputesSurfaceIdentity,
            otherAdministrator.reviewDisputesSurfaceIdentity
        )
        XCTAssertNotEqual(
            otherAdministrator.reviewDisputesSurfaceIdentity,
            siteModerator.reviewDisputesSurfaceIdentity
        )
        XCTAssertNotEqual(
            siteModerator.reviewDisputesSurfaceIdentity,
            member.reviewDisputesSurfaceIdentity
        )
        XCTAssertNotEqual(
            member.reviewDisputesSurfaceIdentity,
            signedOut.reviewDisputesSurfaceIdentity
        )
        XCTAssertNotEqual(
            administrator.reviewDisputesSurfaceIdentity,
            replacementAdministrator.reviewDisputesSurfaceIdentity
        )
    }
}

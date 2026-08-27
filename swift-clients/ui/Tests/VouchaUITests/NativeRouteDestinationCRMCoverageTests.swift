import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteDestinationCRMCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testCrmDestinationUsesNativeSurfaceWhenClientExists() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/crm"))
        let sut = try NativeRouteDestinationView(entry: route.entry, client: makeClient(), routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(button: "Create contact"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Import CSV"))
    }

    func testListsRouteUsesDedicatedListsSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/lists"))
        let sut = NativeRouteDestinationView(
            entry: route.entry,
            client: nil,
            routeMatch: route.match,
            isSignedIn: false
        )

        XCTAssertEqual(try sut.inspect().find(text: "Sign in required").string(), "Sign in required")
        XCTAssertEqual(
            try sut.inspect().find(text: "Sign in to manage your lists.").string(),
            "Sign in to manage your lists."
        )
    }
}

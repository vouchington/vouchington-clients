import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteCRMTests: NativeRouteSurfaceViewModelTestCase {
    func testDestinationViewRendersCrmSurface() throws {
        let sut = try NativeRouteDestinationView(entry: entry(for: .crmContacts))

        XCTAssertEqual(try sut.inspect().find(text: "CRM").string(), "CRM")
        XCTAssertEqual(
            try sut.inspect().find(text: "CRM contacts are available to signed-in staff users.").string(),
            "CRM contacts are available to signed-in staff users."
        )
    }

}

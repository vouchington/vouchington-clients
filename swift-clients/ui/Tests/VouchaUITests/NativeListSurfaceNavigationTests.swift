import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeListSurfaceNavigationTests: NativeRouteSurfaceViewModelTestCase {
    func testTappingARowWithATargetPathInvokesOnNavigate() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .usersBrowse), client: nil)
        viewModel.state = .loaded
        viewModel.rows = [
            NativeRouteDestinationRow(
                id: "users-browse:user-1",
                icon: "person.badge.shield.checkmark",
                title: .verbatim("alice"),
                detail: .verbatim("user"),
                targetPath: "/user/alice/admin"
            )
        ]
        var captured: String?
        let sut = NativeListSurface(viewModel: viewModel) { path in captured = path }

        try sut.inspect().find(button: "alice").tap()

        XCTAssertEqual(captured, "/user/alice/admin")
    }
}

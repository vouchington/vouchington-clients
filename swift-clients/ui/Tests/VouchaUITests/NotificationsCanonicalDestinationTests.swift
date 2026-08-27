import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NotificationsCanonicalDestinationTests: NativeRouteSurfaceViewModelTestCase {
    func testCanonicalDestinationHasSettingsAction() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .notifications), client: nil)
        var navigatedPath: String?
        let sut = NativeListSurface(viewModel: viewModel) { navigatedPath = $0 }

        let settingsButton = try sut.inspect().vStack().toolbar().item().button()
        try settingsButton.tap()

        XCTAssertEqual(navigatedPath, "/my/notification-settings")
    }

    func testOtherDestinationsDoNotExposeSettingsAction() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .compare), client: nil)
        let sut = NativeListSurface(viewModel: viewModel)

        XCTAssertThrowsError(try sut.inspect().vStack().toolbar().item().button())
    }
}

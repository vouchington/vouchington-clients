import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class StaffSupportSurfaceTests: XCTestCase {
    func testConfirmationUsesActionSpecificLocalizedTitles() {
        XCTAssertEqual(
            StaffSupportConfirmation.setResolved(true).titleKey.rawValue,
            "native.swift.support.resolveThreadConfirmationTitle"
        )
        XCTAssertEqual(
            StaffSupportConfirmation.setResolved(false).titleKey.rawValue,
            "native.swift.support.reopenThreadConfirmationTitle"
        )
        XCTAssertEqual(
            StaffSupportConfirmation.setResolved(true).buttonKey.rawValue,
            "native.swift.communityActions.resolve"
        )
    }

    func testThreadSurfaceRendersNativeSearchAndStatusControls() throws {
        let surface = StaffSupportSurface(
            client: nil,
            administratorId: "00000000-0000-7000-8000-000000000001",
            mode: .threads,
            routeMatch: nil
        )

        XCTAssertNoThrow(try surface.inspect().find(text: "Search"))
        XCTAssertNoThrow(try surface.inspect().find(text: "Status"))
    }

    func testContactSurfaceDoesNotRenderThreadStatusFilter() throws {
        let surface = StaffSupportSurface(
            client: nil,
            administratorId: "00000000-0000-7000-8000-000000000001",
            mode: .contacts,
            routeMatch: nil
        )

        XCTAssertNoThrow(try surface.inspect().find(text: "Search"))
        XCTAssertThrowsError(try surface.inspect().find(text: "Status"))
    }

    func testSurfaceUsesNavigationSplitViewForAdaptiveColumns() throws {
        let surface = StaffSupportSurface(
            client: nil,
            administratorId: "00000000-0000-7000-8000-000000000001",
            mode: .threads,
            routeMatch: nil
        )

        let splitView = try surface.inspect().find(ViewType.NavigationSplitView.self)
        XCTAssertNoThrow(try splitView.sidebarView().find(text: "Search"))
    }

    func testSizeClassTransitionsRevealOnlyAnExistingCompactDetailSelection() {
        let state = StaffSupportColumnState()

        state.update(for: .compact, hasDetailSelection: false)
        XCTAssertEqual(state.visibility, .all)
        state.update(for: .compact, hasDetailSelection: true)
        XCTAssertEqual(state.visibility, .detailOnly)
        state.update(for: .regular, hasDetailSelection: true)
        XCTAssertEqual(state.visibility, .all)
    }
}

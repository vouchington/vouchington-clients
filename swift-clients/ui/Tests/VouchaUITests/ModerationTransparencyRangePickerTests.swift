import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationTransparencyRangePickerTests: NativeRouteSurfaceViewModelTestCase {
    func testPickerNavigatesAcrossRanges() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: nil,
            routeMatch: route.match,
            routeQuery: "range=7d"
        )
        var navigatedPath: String?
        let sut = NativeListSurface(viewModel: viewModel) { navigatedPath = $0 }
        let picker = try sut.inspect().find(ViewType.Picker.self)

        XCTAssertEqual(try picker.selectedValue(ModerationTransparencyRange.self), .days7)
        try picker.select(value: ModerationTransparencyRange.all)
        XCTAssertEqual(navigatedPath, "/moderation-transparency?range=all")
        try picker.select(value: ModerationTransparencyRange.days30)
        XCTAssertEqual(navigatedPath, "/moderation-transparency")
    }

    func testTodayUsesLatestReleasedDayLabel() throws {
        let picker = ModerationTransparencyRangePicker(range: .today) { _ in }

        XCTAssertNoThrow(try picker.inspect().find(text: "Latest released day"))
    }
}

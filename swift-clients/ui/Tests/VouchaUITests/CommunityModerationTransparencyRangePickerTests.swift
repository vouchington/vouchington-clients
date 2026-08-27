import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityModerationTransparencyRangePickerTests: NativeRouteSurfaceViewModelTestCase {
    func testPaidCommunityAnalyticsShowsLatestReleasedDayRangePicker() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderationAnalytics
        )

        let picker = try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(ViewType.Picker.self)

        XCTAssertNoThrow(try picker.find(text: "Latest released day"))
    }

    func testRawStaffAnalyticsShowsTodayRangePicker() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderationAnalytics,
            isSiteModerator: true
        )

        let picker = try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(ViewType.Picker.self)

        XCTAssertNoThrow(try picker.find(text: "Today"))
    }

    func testAllTimeCommunityTransparencyExposesLoadOlderAndRetryControls() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderationAnalytics
        )
        viewModel.moderationTransparencyRange = .all
        viewModel.moderationTransparencyNextCursor = "older-page"

        var control = try CommunityWorkspaceSurface(viewModel: viewModel)
            .inspect()
            .find(viewWithAccessibilityIdentifier: "community-moderation-transparency-pagination")
        XCTAssertNoThrow(try control.find(button: "Load more"))

        viewModel.moderationTransparencyNextCursor = nil
        viewModel.moderationTransparencyLoadMoreError = UiMessage(.nativeSwiftRouteSurfaceLoadMoreFailed)
        control = try CommunityWorkspaceSurface(viewModel: viewModel)
            .inspect()
            .find(viewWithAccessibilityIdentifier: "community-moderation-transparency-pagination")
        XCTAssertNoThrow(try control.find(button: UiMessages.string(.nativeCommonRetry, locale: .english)))
    }
}

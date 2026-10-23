import Foundation
@testable import VouchaFeatures
import VouchaTestSupport
import XCTest

extension SettingsMembershipLifecycleTests: CompleteSettingsResponseSeeding {}

@MainActor
final class SettingsMembershipLifecycleTests: NativeRouteSurfaceViewModelTestCase {
    func testLifecycleFixtureStaysFreeAndShowsPendingOutcomesAsText() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = try (
            fixture("native.memberships.me.lifecycle.default"),
            200
        )
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.membershipSummaryText, "Current plan: Free · No active membership")
        XCTAssertTrue(viewModel.membershipPlanPresentations[0].isCurrent)
        XCTAssertFalse(viewModel.membershipPlanPresentations[2].isCurrent)
        XCTAssertEqual(viewModel.membershipPlanPresentations[1].priceSummaryText, "Price unavailable")
        let lines = viewModel.membershipLifecycleLineTexts.joined(separator: "\n")
        XCTAssertTrue(lines.contains("automatic_refund"))
        XCTAssertTrue(lines.contains("competing_direct_source"))
        XCTAssertTrue(lines.contains("wrong_account"))
        XCTAssertTrue(lines.contains("2027-01-01T00:00:00.000Z"))
        XCTAssertTrue(lines.contains("apple_subscriptions"))
        XCTAssertFalse(lines.contains("http"))
        XCTAssertFalse(lines.contains("500"))

        viewModel.noteMembershipOverviewStale()
        XCTAssertEqual(
            viewModel.membershipLifecycleLineTexts.first,
            "Native purchase and billing management are not available yet."
        )
        XCTAssertEqual(viewModel.membershipSummaryText, "Current plan: Free · No active membership")
    }

    func testPurchasePendingReasonStaysPendingWhenStatusIsRejected() async throws {
        seedSettingsResponses()
        let body = """
        {"management":null,"membership":null,"pending":{"financial_operations":[],"grants":0,"switches":[],
        "verifications":[{"id":"v1","next_processing_at":null,"provider":"apple_app_store",
        "reason_code":"purchase_pending","status":"rejected"}]},"sources":[]}
        """
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (Data(body.utf8), 200)
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        let lines = viewModel.membershipLifecycleLineTexts.joined(separator: "\n")
        XCTAssertTrue(lines.contains("Native billing pending"))
        XCTAssertTrue(lines.contains("purchase_pending"))
        XCTAssertTrue(lines.contains("rejected"))
    }

    private func fixture(_ name: String) throws -> Data {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/manifest.json"])
        let url = try FilamentsContractRoot.requiredURL("api-fixtures/v1/responses/\(name).json", root: root)
        return try Data(contentsOf: url)
    }
}

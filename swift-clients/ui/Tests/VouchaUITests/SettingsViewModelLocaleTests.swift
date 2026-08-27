import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsViewModelLocaleTests: NativeRouteSurfaceViewModelTestCase {
    func testPrivacySavePreservesServerDefaultLocale() async throws {
        seedLocaleSettingsResponses(uiLocaleJSON: "null")
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        viewModel.followsVisibility = .everyone
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1"] = (
            PrivateUserTestFixture.userEnvelope(
                profileImageId: "image-1",
                overrides: settingsOverrides(uiLocale: NSNull(), followsVisibility: "everyone")
            ),
            200
        )

        await viewModel.savePrivacy()

        XCTAssertEqual(viewModel.uiLocale, SettingsViewModel.siteDefaultUiLocale)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.last??.contains(#""follows_visibility":"everyone""#) == true)
        XCTAssertFalse(CannedFeedURLProtocol.capturedBodies.last??.contains(#""ui_locale""#) == true)
    }

    func testPrivacySaveCanRestoreServerDefaultLocale() async throws {
        seedLocaleSettingsResponses(uiLocaleJSON: #""fr""#)
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        viewModel.uiLocale = SettingsViewModel.siteDefaultUiLocale
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1"] = (
            PrivateUserTestFixture.userEnvelope(
                profileImageId: "image-1",
                overrides: settingsOverrides(uiLocale: NSNull(), followsVisibility: "followers")
            ),
            200
        )

        await viewModel.savePrivacy()

        XCTAssertEqual(viewModel.uiLocale, SettingsViewModel.siteDefaultUiLocale)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.last??.contains(#""ui_locale":null"#) == true)
    }

    private func seedLocaleSettingsResponses(uiLocaleJSON: String) {
        let uiLocale: Any = uiLocaleJSON == "null"
            ? NSNull()
            : String(uiLocaleJSON.dropFirst().dropLast())
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                profileImageId: "image-1",
                overrides: settingsOverrides(uiLocale: uiLocale, followsVisibility: "followers")
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"profile":{"id":"user-1","markdown":"Native bio"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/push-subscriptions"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/sessions"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (Data(#"{"membership":null}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/memberships/plans"] = (Data(#"{"plans":{}}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/data-request"] = (Data("{}".utf8), 404)
    }

    private func settingsOverrides(uiLocale: Any, followsVisibility: String) -> [String: Any] {
        [
            "use_display_name_from": "github",
            "cards_visibility": "users",
            "rewards_program_statuses_visibility": "users",
            "spending_categories_visibility": "nobody",
            "follows_visibility": followsVisibility,
            "community_memberships_visibility": "users",
            "followers_visibility": "followers",
            "ui_locale": uiLocale,
            "default_post_broadcast": "followers",
            "default_post_privacy": "private",
            "processing_restricted_at": "2026-03-01T00:00:00Z",
            "third_party_marketing": true
        ]
    }
}

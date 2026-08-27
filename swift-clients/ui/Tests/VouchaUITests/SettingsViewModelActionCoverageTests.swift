import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsViewModelActionCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testIdentityPrivacyAndProfileActionsSendExpectedRequests() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        viewModel.username = "alice-2"
        viewModel.displayNameSource = .github
        viewModel.profileImageId = "image-2"
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                username: "alice-2",
                profileImageId: "image-2",
                overrides: settingsOverrides(uiLocale: "en", followsVisibility: "followers")
            ),
            200
        )

        await viewModel.saveIdentity()

        XCTAssertEqual(viewModel.username, "alice-2")
        XCTAssertEqual(viewModel.profileImageId, "image-2")
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Identity saved")

        viewModel.followsVisibility = .everyone
        viewModel.cardsVisibility = .users
        viewModel.uiLocale = "fr"
        viewModel.processingRestrictedAt = true
        viewModel.thirdPartyMarketing = false
        var privacyOverrides = settingsOverrides(
            uiLocale: "fr",
            followsVisibility: "everyone",
            thirdPartyMarketing: false
        )
        privacyOverrides["is_official_account"] = true
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1"] = (
            PrivateUserTestFixture.userEnvelope(
                username: "alice-2",
                profileImageId: "image-2",
                overrides: privacyOverrides
            ),
            200
        )

        await viewModel.savePrivacy()

        XCTAssertEqual(viewModel.followsVisibility, .everyone)
        XCTAssertEqual(viewModel.uiLocale, "fr")
        XCTAssertFalse(viewModel.thirdPartyMarketing)
        XCTAssertTrue(viewModel.identity?.isOfficialAccount == true)
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Privacy saved")
        XCTAssertTrue(
            CannedFeedURLProtocol.capturedBodies.last??.contains(#""ui_locale":"fr""#) == true
        )

        viewModel.profileMarkdown = "Updated bio"
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"profile":{"id":"user-1","markdown":"Updated bio"}}"#.utf8),
            200
        )

        await viewModel.saveProfile()

        XCTAssertEqual(viewModel.profileMarkdown, "Updated bio")
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Profile saved")
    }

    func testProfileLinkActionsCreateAndUpdateEntries() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        viewModel.profileLinkType = .github
        viewModel.profileLinkURL = "https://github.com/voucha"
        viewModel.profileLinkHandle = "octocat"
        viewModel.profileLinkName = "Voucha"
        viewModel.profileLinkImageId = "image-3"
        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links"] = (
            Data("""
            {
              "profile_link": {
                "id": "link-2",
                "user_id": "user-1",
                "link_type": "github",
                "sort_order": 1,
                "url_id": null,
                "url": "https://github.com/voucha",
                "handle": "octocat",
                "name": "Voucha",
                "image_id": "image-3",
                "created_at": "2026-03-01T10:00:00Z",
                "updated_at": "2026-03-01T10:00:00Z"
              }
            }
            """.utf8),
            201
        )

        await viewModel.createProfileLink()

        XCTAssertEqual(viewModel.profileLinks.count, 2)
        XCTAssertEqual(viewModel.profileLinks.last?.id, "link-2")
        XCTAssertEqual(viewModel.profileLinkURL, "")
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Profile link added")

        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links/link-1"] = (
            Data("""
            {
              "profile_link": {
                "id": "link-1",
                "user_id": "user-1",
                "link_type": "github",
                "sort_order": 0,
                "url_id": null,
                "url": "https://example.com",
                "handle": "alice",
                "name": "Updated",
                "image_id": null,
                "created_at": "2026-03-01T10:00:00Z",
                "updated_at": "2026-03-01T10:00:00Z"
              }
            }
            """.utf8),
            200
        )

        await viewModel.updateProfileLink(
            id: "link-1",
            url: "https://example.com",
            handle: "alice",
            name: "Updated",
            imageId: nil
        )

        XCTAssertEqual(viewModel.profileLinks.first?.name, "Updated")
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Profile link updated")
    }

    func testManagementActionsCoverMcpApiKeysRevokeAndDeletePaths() async throws {
        seedSettingsResponses()
        var didRequestLogout = false
        let viewModel = try SettingsViewModel(client: makeClient()) {
            didRequestLogout = true
        }
        await viewModel.load()

        viewModel.apiKeyLabel = "Agent"
        viewModel.apiKeyType = .mcp
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            Data("""
            {
              "api_key": {
                "id": "key-2",
                "user_id": "user-1",
                "prefix": "voucha_mcp_abcd",
                "type": "mcp",
                "label": "Agent",
                "permissions": ["mcp-tools:read", "mcp-tools:write"],
                "created_at": "2026-03-01T10:00:00Z",
                "last_used_at": null,
                "revoked_at": null,
                "updated_at": "2026-03-01T10:00:00Z"
              },
              "raw_key": "voucha_mcp_raw"
            }
            """.utf8),
            201
        )

        await viewModel.createApiKey()

        XCTAssertEqual(viewModel.apiKeys.first?.type, .mcp)
        XCTAssertEqual(viewModel.latestRawAPIKey, "voucha_mcp_raw")
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "API key created")

        CannedFeedURLProtocol.handlers["/api/v1/auth/sessions/session-1"] = (Data("{}".utf8), 204)
        await viewModel.revokeSession(id: "session-1")

        XCTAssertFalse(viewModel.sessions.contains { $0.id == "session-1" })
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Current session revoked")
        XCTAssertTrue(didRequestLogout)

        didRequestLogout = false
        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/auth/sessions/revocations"] = (Data("{}".utf8), 204)
        await viewModel.revokeAllSessions()

        XCTAssertTrue(viewModel.sessions.isEmpty)
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "All sessions revoked")
        XCTAssertTrue(didRequestLogout)

        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys/key-1"] = (Data("{}".utf8), 204)
        await viewModel.revokeApiKey(id: "key-1")

        XCTAssertFalse(viewModel.apiKeys.contains { $0.id == "key-1" })
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "API key revoked")

        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/push-subscriptions/sub-1"] = (
            Data("{}".utf8),
            204
        )
        await viewModel.revokePushSubscription(id: "sub-1")

        XCTAssertFalse(viewModel.pushSubscriptions.contains { $0.id == "sub-1" })
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Push subscription revoked")

        didRequestLogout = false
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1"] = (
            Data(#"{"logout":false}"#.utf8),
            200
        )
        viewModel.deleteConfirmation = "delete my account"
        await viewModel.deleteAccount()

        XCTAssertFalse(didRequestLogout)
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Account deletion requested")
    }

    private func seedSettingsResponses(uiLocaleJSON: String = #""en""#) {
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
            Data("""
            {
              "results": [
                {
                  "id": "link-1",
                  "user_id": "user-1",
                  "link_type": "github",
                  "sort_order": 0,
                  "url_id": null,
                  "url": null,
                  "handle": "alice",
                  "name": "Alice",
                  "image_id": null,
                  "created_at": "2026-03-01T10:00:00Z",
                  "updated_at": "2026-03-01T10:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            Data("""
            {
              "results": [
                {
                  "id": "key-1",
                  "user_id": "user-1",
                  "prefix": "voucha_rss_abcd",
                  "type": "rss",
                  "label": "Reader",
                  "permissions": ["rss-feeds:read"],
                  "created_at": "2026-03-01T10:00:00Z",
                  "last_used_at": null,
                  "revoked_at": null,
                  "updated_at": "2026-03-01T10:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/push-subscriptions"] = (
            Data("""
            {
              "results": [
                {
                  "id": "sub-1",
                  "user_id": "user-1",
                  "endpoint": "https://push.example.com",
                  "p256dh": "abcdabcdabcdabcd",
                  "auth": "abcdefgh",
                  "expiration_time_ms": null,
                  "user_agent": "Safari",
                  "last_success_at": null,
                  "last_failure_at": null,
                  "created_at": "2026-03-01T10:00:00Z",
                  "updated_at": "2026-03-01T10:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/sessions"] = (
            Data("""
            {
              "results": [
                {
                  "id": "session-1",
                  "device_id": "device-1",
                  "device_name": "MacBook Pro",
                  "user_agent": "Safari",
                  "ip_address": "203.0.113.8",
                  "created_at": "2026-03-01T10:00:00Z",
                  "last_seen_at": "2026-03-01T12:00:00Z",
                  "expires_at": "2026-03-31T10:00:00Z",
                  "is_current": true
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (
            Data("""
            {
              "membership": {
                "id": "mem-1",
                "user_id": "user-1",
                "plan": "pro",
                "status": "active",
                "started_at": "2026-03-01T10:00:00Z",
                "expires_at": null,
                "granted_by_id": null,
                "cancelled_at": null,
                "expired_at": null,
                "past_due_at": null,
                "paused_at": null,
                "cancel_at_period_end": false,
                "latest_change_id": null,
                "created_at": "2026-03-01T10:00:00Z",
                "updated_at": "2026-03-01T10:00:00Z",
                "sku": {
                  "id": "sku-1",
                  "plan": "pro",
                  "price": { "amount": 1299, "currency": "usd" },
                  "interval": "monthly",
                  "stripe_price_id": "price-pro",
                  "retired_at": null
                },
                "has_stripe_subscription": true
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/memberships/plans"] = (
            Data("""
            {
              "plans": {
                "pro": [
                  {
                    "id": "sku-1",
                    "plan": "pro",
                    "price": { "amount": 1299, "currency": "usd" },
                    "interval": "monthly",
                    "stripe_price_id": "price-pro",
                    "retired_at": null
                  }
                ]
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/data-request"] = (
            Data("""
            {
              "id": "req-1",
              "status": "ready",
              "created_at": "2026-03-01T10:00:00Z",
              "expires_at": "2027-03-08T10:00:00Z",
              "download_url": "https://example.com/export.zip"
            }
            """.utf8),
            200
        )
    }

    private func settingsOverrides(
        uiLocale: Any,
        followsVisibility: String,
        thirdPartyMarketing: Bool = true
    ) -> [String: Any] {
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
            "third_party_marketing": thirdPartyMarketing
        ]
    }
}

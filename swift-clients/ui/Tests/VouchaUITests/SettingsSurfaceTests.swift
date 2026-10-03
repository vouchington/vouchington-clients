import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testSettingsSurfaceRendersFunctionalSections() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        viewModel.statusMessage = .verbatim("Saved")
        viewModel.latestRawAPIKey = "voucha_rss_raw"

        let sut = SettingsSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Saved"))
        XCTAssertNoThrow(try sut.inspect().find(text: "API key shown once"))
        XCTAssertNoThrow(try sut.inspect().find(text: "voucha_rss_raw"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Account"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Profile"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Privacy"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Legal and Support"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Membership"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Current plan: Pro · Active"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Free"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Plus"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Pro"))
        XCTAssertNoThrow(try sut.inspect().find(text: "$12.99"))
        XCTAssertNoThrow(try sut.inspect().find(text: "$99.99"))
        XCTAssertNoThrow(try sut.inspect().find(text: "API Keys"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Reader"))
        XCTAssertNoThrow(try sut.inspect().find(text: "voucha_rss_abcd"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Push Subscriptions"))
        XCTAssertNoThrow(try sut.inspect().find(text: "https://push.example.com"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Safari"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Sessions"))
        XCTAssertNoThrow(try sut.inspect().find(text: "MacBook Pro"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Sign Out"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Revoke All Sessions"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Your Data"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Export: Ready"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Alice"))
        XCTAssertNoThrow(try sut.inspect().find(text: "GitHub"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Interface language"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save Identity"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Clear Avatar"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save Bio"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Add Link"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save Privacy"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Privacy Policy"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Terms of Service"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Community Guidelines"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Support"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Current plan"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Native billing pending"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Included"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Create API Key"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Revoke"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Request Data Export"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Delete Account"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Billing Portal"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Cancel Membership"))
    }

    func testSettingsSurfaceRendersLocalLLMSectionWhenAvailable() async throws {
        seedSettingsResponses()
        let endpointID = UUID()
        let store = await makeLocalLLMSettingsStore(
            configuration: makeLocalLLMConfiguration(endpointID: endpointID)
        )
        let viewModel = try SettingsViewModel(
            client: makeClient(),
            localLLMSettingsStore: store,
            localLLMFeaturePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            localLLMResponsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )
        await viewModel.load()
        viewModel.localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaved)

        let sut = SettingsSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Local Models"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Local model settings saved."))
        XCTAssertNoThrow(try sut.inspect().find(button: "Add Local Model"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Test"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Unnamed local model"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Current"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Clear"))
    }

    func testSettingsSurfaceRoutesLegalSupportActionsThroughNativeRouter() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        var navigatedPaths: [String] = []
        let sut = SettingsSurface(
            viewModel: viewModel,
            onNavigateToTargetPath: { navigatedPaths.append($0) }
        )

        try sut.inspect().find(button: "Privacy Policy").tap()
        try sut.inspect().find(button: "Terms of Service").tap()
        try sut.inspect().find(button: "Community Guidelines").tap()

        XCTAssertEqual(
            navigatedPaths,
            [
                "/article/privacy-policy",
                "/article/terms-of-service",
                "/article/community-guidelines"
            ]
        )
    }

    func testSettingsSurfaceRendersLoadingAndInitialErrorBranches() throws {
        let viewModel = try SettingsViewModel(client: makeClient())

        viewModel.state = .loading
        let loadingSurface = SettingsSurface(viewModel: viewModel)
        XCTAssertNoThrow(try loadingSurface.inspect().find(ViewType.ProgressView.self))

        viewModel.state = .error(.api(statusCode: 500, preconditionCode: "SETTINGS_DOWN"))
        let errorSurface = SettingsSurface(viewModel: viewModel)
        XCTAssertNoThrow(try errorSurface.inspect().find(text: "Something went wrong"))
        XCTAssertNoThrow(try errorSurface.inspect().find(text: "API error: SETTINGS_DOWN"))
        XCTAssertNoThrow(try errorSurface.inspect().find(button: "Try Again"))
    }

    func testFocusedNotificationHeadingRemainsRenderedDuringInitialSettingsLoadAndAfterRemount() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (
            Data(
                """
                {
                  "email_preferences": {
                    "engagement_emails_enabled": true,
                    "news_digest_frequency": "weekly",
                    "moderation_emails_enabled": true,
                    "community_digest_frequency": "weekly",
                    "moderation_email_cadence": "daily",
                    "moderation_email_days_of_week": [1, 2, 3],
                    "moderation_email_time_of_day": "09:00",
                    "moderation_email_timezone": "UTC"
                  }
                }
                """.utf8
            ),
            200
        )
        let notificationViewModel = try NotificationSettingsViewModel(client: makeClient())
        await notificationViewModel.load()
        XCTAssertNotNil(notificationViewModel.current)
        let path = "/api/v1/my/identity"
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let viewModel = try SettingsViewModel(client: makeClient())
        let load = Task { await viewModel.load() }
        await waitForSuspendedResponse(path)
        XCTAssertTrue(viewModel.isLoading)

        let loadingSurface = SettingsSurface(
            viewModel: viewModel,
            focusedSection: .notifications,
            notificationSettingsViewModel: notificationViewModel
        )
        XCTAssertNoThrow(
            try loadingSurface.inspect().find(viewWithAccessibilityIdentifier: "notification-settings-heading")
        )
        XCTAssertThrowsError(try loadingSurface.inspect().find(ViewType.ProgressView.self))

        CannedFeedURLProtocol.releaseResponse(path: path)
        await load.value
        let remountedSurface = SettingsSurface(
            viewModel: viewModel,
            focusedSection: .notifications,
            notificationSettingsViewModel: notificationViewModel
        )
        XCTAssertNoThrow(
            try remountedSurface.inspect().find(viewWithAccessibilityIdentifier: "notification-settings-heading")
        )
    }

    func testSettingsSurfaceShowsExportLinkAndGatesDeleteConfirmation() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        let sut = SettingsSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Download Data Export"))
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Link.self).count, 2)
        XCTAssertTrue(try sut.inspect().find(button: "Delete Account").isDisabled())

        viewModel.deleteConfirmation = "  DELETE my account  "
        let updatedSut = SettingsSurface(viewModel: viewModel)

        XCTAssertFalse(try updatedSut.inspect().find(button: "Delete Account").isDisabled())
    }

    func testMembershipPlanButtonsAreDisabled() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        let sut = SettingsSurface(viewModel: viewModel)

        XCTAssertTrue(try sut.inspect().find(button: "Native billing pending").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "Current plan").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "Included").isDisabled())
    }

    func testSessionActionsRequireConfirmationBeforeMutatingSessions() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        let sut = SettingsSurface(viewModel: viewModel)

        try sut.inspect().find(button: "Sign Out").tap()
        XCTAssertEqual(viewModel.sessions.count, 1)

        try sut.inspect().find(button: "Revoke All Sessions").tap()
        XCTAssertEqual(viewModel.sessions.count, 1)
    }

    func testProfileLinkEditorRendersLoadedDraft() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        let link = try XCTUnwrap(viewModel.profileLinks.first)

        let sut = ProfileLinkEditor(link: link, viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Alice"))
        XCTAssertNoThrow(try sut.inspect().find(text: "GitHub"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Delete"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save"))
    }

    func testLocalLLMEndpointEditorRendersUnnamedProfileWithActivateWhenNotSelected() async throws {
        let endpointID = UUID()
        let store = await makeLocalLLMSettingsStore(
            configuration: makeLocalLLMConfiguration(endpointID: endpointID)
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        await viewModel.loadLocalLLMSettings()
        let profile = try XCTUnwrap(viewModel.localLLMEndpoints.first)

        let sut = LocalLLMEndpointEditor(profile: profile, isSelected: false, viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Unnamed local model"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Current"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Activate"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Delete"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Test"))
    }

    func testLocalLLMEndpointEditorRendersSelectedNamedProfileWithCurrentBadge() async throws {
        let endpointID = UUID()
        let configuration = LocalLLMConfiguration(
            endpoints: [
                LocalLLMEndpointProfile(
                    id: endpointID,
                    displayName: "My Local Model",
                    endpoint: "http://localhost:2999/v1"
                )
            ],
            selectedEndpointID: endpointID
        )
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        await viewModel.loadLocalLLMSettings()
        let profile = try XCTUnwrap(viewModel.localLLMEndpoints.first)

        let sut = LocalLLMEndpointEditor(profile: profile, isSelected: true, viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "My Local Model"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Current"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Activate"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Delete"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Test"))
    }

    private func seedSettingsResponses() {
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (SettingsCredentialsTestData.grants([]), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                profileImageId: "image-1",
                overrides: [
                    "use_display_name_from": "github",
                    "cards_visibility": "users",
                    "rewards_program_statuses_visibility": "users",
                    "spending_categories_visibility": "nobody",
                    "follows_visibility": "followers",
                    "community_memberships_visibility": "users",
                    "followers_visibility": "followers",
                    "ui_locale": "en",
                    "default_post_broadcast": "followers",
                    "default_post_privacy": "private",
                    "processing_restricted_at": "2026-03-01T00:00:00Z",
                    "third_party_marketing": true
                ]
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
            Data(
                #"{"products":[{"id":"sku-1","plan":"plus","interval":"monthly","providers":[{"provider":"stripe","environment":"test","application_id":"voucha-web","product_id":"price-plus-monthly","base_plan_id":null,"offer_id":null,"sku_id":null,"price":{"amount":1299,"currency":"usd"}}]},{"id":"sku-2","plan":"plus","interval":"yearly","providers":[{"provider":"stripe","environment":"test","application_id":"voucha-web","product_id":"price-plus-yearly","base_plan_id":null,"offer_id":null,"sku_id":null,"price":{"amount":9999,"currency":"usd"}}]},{"id":"sku-3","plan":"pro","interval":"monthly","providers":[{"provider":"stripe","environment":"test","application_id":"voucha-web","product_id":"price-pro-monthly","base_plan_id":null,"offer_id":null,"sku_id":null,"price":{"amount":2499,"currency":"usd"}}]}]}"#
                    .utf8
            ),
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

    private func waitForSuspendedResponse(_ path: String) async {
        for _ in 0 ..< 100 where !CannedFeedURLProtocol.hasSuspendedResponse(path: path) {
            await Task.yield()
        }
        XCTAssertTrue(CannedFeedURLProtocol.hasSuspendedResponse(path: path))
    }
}

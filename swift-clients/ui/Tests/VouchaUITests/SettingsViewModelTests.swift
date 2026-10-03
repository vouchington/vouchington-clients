import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testLoadHydratesSettingsData() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.username, "alice")
        XCTAssertEqual(viewModel.profileMarkdown, "Native bio")
        XCTAssertEqual(viewModel.profileLinks.count, 1)
        XCTAssertEqual(viewModel.apiKeys.count, 1)
        XCTAssertEqual(viewModel.pushSubscriptions.count, 1)
        XCTAssertEqual(viewModel.sessions.count, 1)
        XCTAssertEqual(viewModel.membership?.plan, "pro")
        XCTAssertEqual(viewModel.membershipPlans["pro"]?.first?.price.amount, 2_499)
        XCTAssertEqual(viewModel.dataRequest?.status, .ready)
        XCTAssertEqual(viewModel.followsVisibility, .followers)
        XCTAssertEqual(viewModel.uiLocale, "en")
    }

    func testMembershipPlanPresentationsDeriveCurrentPlanAndPricing() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        let cards = viewModel.membershipPlanPresentations

        XCTAssertEqual(cards.map(\.id), ["free", "plus", "pro"])
        XCTAssertEqual(cards[0].title, "Free")
        XCTAssertEqual(cards[0].purchaseButtonTitle, "Included")
        XCTAssertEqual(cards[1].priceOptions.map(\.priceText), ["$12.99", "$99.99"])
        XCTAssertEqual(cards[1].priceOptions.map(\.intervalText), ["Monthly", "Yearly"])
        XCTAssertEqual(cards[2].isCurrent, true)
        XCTAssertEqual(cards[2].currentStateText, "Active")
        XCTAssertTrue(cards[2].featureBullets.contains("AI moderation rules: 10"))
        XCTAssertTrue(cards[1].featureBullets.contains("Contribution capacity: More"))
        XCTAssertTrue(cards[1].featureBullets.contains("Automatic post topics: More"))
        XCTAssertTrue(cards[1].featureBullets.contains("Post downvote counts: Included"))
        XCTAssertTrue(cards[1].featureBullets.contains("Support service level: Priority"))
        XCTAssertTrue(cards[2].featureBullets.contains("Contribution capacity: Most"))
        XCTAssertTrue(cards[2].featureBullets.contains("Support service level: Highest priority"))
        XCTAssertFalse(cards.flatMap(\.featureBullets).contains { $0.localizedCaseInsensitiveContains("vote weight") })
        XCTAssertEqual(cards[1].purchaseButtonTitle, "Native billing pending")
        XCTAssertEqual(cards[1].purchaseButtonHint, "Native purchase and billing management are not available yet.")
        XCTAssertEqual(viewModel.membershipSummaryText, "Current plan: Pro · Active")
    }

    func testMembershipPlanPresentationsUseFreeWhenMembershipIsMissing() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (
            Data(#"{"membership":null}"#.utf8),
            200
        )
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.membershipSummaryText, "Current plan: Free · No active membership")
        XCTAssertTrue(viewModel.membershipPlanPresentations[0].isCurrent)
        XCTAssertFalse(viewModel.membershipPlanPresentations[2].isCurrent)
    }

    func testMembershipPlanPresentationsTreatPausedMembershipAsFreeEntitlement() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (
            Data("""
            {
              "membership": {
                "id": "mem-1",
                "user_id": "user-1",
                "plan": "pro",
                "status": "paused",
                "started_at": "2026-03-01T10:00:00Z",
                "expires_at": null,
                "granted_by_id": null,
                "cancelled_at": null,
                "expired_at": null,
                "past_due_at": null,
                "paused_at": "2026-04-01T10:00:00Z",
                "cancel_at_period_end": false,
                "latest_change_id": null,
                "created_at": "2026-03-01T10:00:00Z",
                "updated_at": "2026-04-01T10:00:00Z",
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
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.membershipSummaryText, "Current plan: Free · Paused")
        XCTAssertTrue(viewModel.membershipPlanPresentations[0].isCurrent)
        XCTAssertFalse(viewModel.membershipPlanPresentations[2].isCurrent)
    }

    func testApiKeyCreationExposesRawKeyOnce() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            Data("""
            {
              "api_key": {
                "id": "key-2",
                "user_id": "user-1",
                "prefix": "voucha_rss_abcd",
                "type": "rss",
                "label": "Reader",
                "permissions": ["rss-feeds:read"],
                "created_at": "2026-03-01T10:00:00Z",
                "last_used_at": null,
                "revoked_at": null,
                "updated_at": "2026-03-01T10:00:00Z"
              },
              "raw_key": "voucha_rss_raw"
            }
            """.utf8),
            201
        )
        let viewModel = try SettingsViewModel(client: makeClient())
        viewModel.apiKeyLabel = "Reader"

        await viewModel.loadCredentialSettings()
        viewModel.setApiKeyScope("feed:read", selected: true)

        await viewModel.createApiKey()

        XCTAssertEqual(viewModel.latestRawAPIKey, "voucha_rss_raw")
        XCTAssertEqual(viewModel.apiKeys.first?.label, "Reader")
    }

    func testClearProfileImageClearsAvatarState() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        viewModel.username = "unsaved-alice"
        viewModel.displayNameSource = .username
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                profileImageId: nil,
                overrides: ["use_display_name_from": "github"]
            ),
            200
        )

        await viewModel.clearProfileImage()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/my/identity")
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.last??.contains(#""username":"alice""#) == true)
        XCTAssertTrue(
            CannedFeedURLProtocol.capturedBodies.last??.contains(#""use_display_name_from":"github""#) == true
        )
        XCTAssertFalse(CannedFeedURLProtocol.capturedBodies.last??.contains("unsaved-alice") == true)
        XCTAssertEqual(viewModel.profileImageId, "")
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Profile image cleared")
    }

    func testLoadUnexpectedErrorPreservesDescription() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (Data("{".utf8), 200)
        let viewModel = try SettingsViewModel(client: makeClient())

        await viewModel.load()

        if case let .error(error) = viewModel.state {
            XCTAssertTrue(error.errorDescription?.contains("Failed to decode response:") == true)
        } else {
            XCTFail("Expected load to fail")
        }
    }

    func testMutationUnexpectedErrorLeavesErrorStateAndPreservesDraftData() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"profile":{"id":"user-1","markdown":"Loaded bio"}}"#.utf8),
            200
        )
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        viewModel.profileMarkdown = "Edited bio"
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (Data("{".utf8), 200)

        await viewModel.saveProfile()

        if case .error = viewModel.state {
            XCTAssertEqual(viewModel.profileMarkdown, "Edited bio")
            XCTAssertTrue(uiEnglish(viewModel.statusMessage)?.contains("Failed to decode response:") == true)
        } else {
            XCTFail("Expected saveProfile to fail")
        }
    }

    func testDeleteAccountLogoutInvokesCallback() async throws {
        seedSettingsResponses()
        var didRequestLogout = false
        let viewModel = try SettingsViewModel(client: makeClient()) {
            didRequestLogout = true
        }
        await viewModel.load()
        viewModel.deleteConfirmation = "delete my account"
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1"] = (
            Data(#"{"logout":true}"#.utf8),
            200
        )

        await viewModel.deleteAccount()

        XCTAssertTrue(didRequestLogout)
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Account deleted")
    }

    func testDeleteAccountRequiresTypedConfirmation() async throws {
        seedSettingsResponses()
        var didRequestLogout = false
        let viewModel = try SettingsViewModel(client: makeClient()) {
            didRequestLogout = true
        }
        await viewModel.load()
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedURLs = []

        await viewModel.deleteAccount()

        XCTAssertFalse(didRequestLogout)
        XCTAssertEqual(uiEnglish(viewModel.statusMessage), "Type delete my account to confirm account deletion.")
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/users/user-1" })
    }

    func testProfileLinkReorderDeleteAndDataRequestActionsWork() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        viewModel.moveProfileLink(id: "link-1", by: 1)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links/order"] = (
            Data("""
            {
              "results": [
                {
                  "id": "link-2",
                  "user_id": "user-1",
                  "link_type": "twitter",
                  "sort_order": 0,
                  "url_id": null,
                  "url": null,
                  "handle": "octocat",
                  "name": "GitHub",
                  "image_id": null,
                  "created_at": "2026-03-01T10:00:00Z",
                  "updated_at": "2026-03-01T10:00:00Z"
                },
                {
                  "id": "link-1",
                  "user_id": "user-1",
                  "link_type": "github",
                  "sort_order": 1,
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
        await viewModel.reorderProfileLinks()

        XCTAssertEqual(viewModel.profileLinks.first?.id, "link-2")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/my/profile/links/order" })

        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links/link-1"] = (Data("{}".utf8), 204)
        await viewModel.deleteProfileLink(id: "link-1")
        XCTAssertFalse(viewModel.profileLinks.contains { $0.id == "link-1" })
    }

    func testMembershipPortalCheckoutAndCancelActionsWork() async throws {
        seedSettingsResponses()
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()

        CannedFeedURLProtocol.handlers["/api/v1/memberships/checkout"] = (
            Data(#"{"checkout_session":{"id":"sess-1","url":"https://checkout.example.com"}}"#.utf8),
            200
        )
        await viewModel.purchaseMembership(priceId: "price-pro")
        XCTAssertEqual(viewModel.membershipCheckoutURL, "https://checkout.example.com")

        CannedFeedURLProtocol.handlers["/api/v1/memberships/billing-portal-sessions"] = (
            Data(#"{"portal_session":{"url":"https://portal.example.com"}}"#.utf8),
            200
        )
        await viewModel.openMembershipPortal()
        XCTAssertEqual(viewModel.membershipPortalURL, "https://portal.example.com")

        CannedFeedURLProtocol.handlers["/api/v1/my/membership"] = (Data("{}".utf8), 204)
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (
            Data(#"{"membership":null}"#.utf8),
            200
        )
        await viewModel.cancelMembership()
        XCTAssertNil(viewModel.membership)
    }

    func testLocalLLMEndpointCreateTestAndDeleteRoundTripsDisplayName() async throws {
        LocalLLMTestURLProtocol.reset(responseData: Data(#"{"output_text":"ready"}"#.utf8))
        let store = await makeLocalLLMSettingsStore()
        let viewModel = try SettingsViewModel(
            client: makeClient(),
            localLLMSettingsStore: store,
            localLLMFeaturePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            localLLMResponsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )
        viewModel.localLLMDisplayName = "My Local Model"
        viewModel.localLLMEndpoint = "http://localhost:2999/v1"
        viewModel.localLLMModelsText = "gpt-oss\n\nllama"
        viewModel.localLLMAPIKey = "stored-key"

        await viewModel.createLocalLLMEndpoint()

        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Local model settings saved.")
        let endpointID = try XCTUnwrap(store.load().selectedEndpointID)
        XCTAssertEqual(store.load().selectedEndpoint?.displayName, "My Local Model")
        XCTAssertEqual(store.load().selectedEndpoint?.normalizedModelNames, ["gpt-oss", "llama"])
        XCTAssertEqual(viewModel.localLLMDisplayName, "")
        XCTAssertEqual(viewModel.localLLMEndpoint, "")

        viewModel.localLLMDisplayName = "Untested draft"
        viewModel.localLLMEndpoint = "http://localhost:2999/v1"
        viewModel.localLLMModelsText = "gpt-oss"
        viewModel.localLLMSelectedModel = "gpt-oss"
        viewModel.localLLMAPIKey = "stored-key"
        await viewModel.testLocalLLMSettings()
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Local model test passed.")
        XCTAssertEqual(
            LocalLLMTestURLProtocol.capturedRequest?.value(forHTTPHeaderField: "Authorization"),
            "Bearer stored-key"
        )

        LocalLLMTestURLProtocol.reset(responseData: Data(#"{"output_text":"  "}"#.utf8))
        await viewModel.testLocalLLMSettings()
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Local model returned an empty response.")

        await viewModel.deleteLocalLLMEndpoint(id: endpointID)
        XCTAssertTrue(store.load().endpoints.isEmpty)
        XCTAssertNil(store.load().selectedEndpointID)
        let clearedAPIKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(clearedAPIKey)
    }

    func testLocalLLMSettingsTestSurfacesTypedErrors() async throws {
        let viewModel = try await SettingsViewModel(
            client: makeClient(),
            localLLMSettingsStore: makeLocalLLMSettingsStore(),
            localLLMFeaturePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            localLLMResponsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )
        viewModel.localLLMEnabled = true
        viewModel.localLLMEndpoint = "not a url"
        viewModel.localLLMModelsText = "gpt-oss"
        viewModel.localLLMSelectedModel = "gpt-oss"

        await viewModel.testLocalLLMSettings()

        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Enter a valid Responses API endpoint.")
    }

    func testLocalLLMEndpointUpdatePreservesOtherProfilesAndExplicitProviderSelection() async throws {
        let editedID = UUID()
        let preservedID = UUID()
        let selectedProviderID = "openai_compatible:\(preservedID.uuidString.lowercased())"
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: editedID,
                    displayName: "Edited",
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["old-model"],
                    selectedModelName: "old-model"
                ),
                LocalLLMEndpointProfile(
                    id: preservedID,
                    displayName: "Preserved",
                    isEnabled: true,
                    endpoint: "https://models.example.test/v1",
                    modelNames: ["remote-model"],
                    selectedModelName: "remote-model"
                )
            ],
            selectedEndpointID: editedID,
            selectedProviderID: selectedProviderID
        )
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let viewModel = try SettingsViewModel(
            client: makeClient(),
            localLLMSettingsStore: store,
            localLLMFeaturePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"])
        )

        await viewModel.updateLocalLLMEndpoint(
            id: editedID,
            draft: LocalLLMEndpointDraft(
                displayName: "Edited",
                isEnabled: true,
                endpoint: "http://localhost:2999/v1",
                modelsText: "new-model",
                selectedModel: "new-model",
                apiKey: ""
            ),
            apiKeyWasEdited: false
        )

        let saved = store.load()
        XCTAssertEqual(saved.endpoints.count, 2)
        XCTAssertEqual(saved.endpoint(id: editedID)?.selectedModel, "new-model")
        XCTAssertEqual(saved.endpoint(id: preservedID)?.selectedModel, "remote-model")
        XCTAssertEqual(saved.selectedProviderID, selectedProviderID)
    }

    func testDisablingLocalLLMEndpointClearsProviderSelection() async throws {
        let endpointID = UUID()
        let providerID = "openai_compatible:\(endpointID.uuidString.lowercased())"
        let store = await makeLocalLLMSettingsStore(configuration: .init(
            isEnabled: true,
            endpoints: [.init(
                id: endpointID,
                isEnabled: true,
                endpoint: "http://localhost:2999/v1",
                modelNames: ["gpt-oss"],
                selectedModelName: "gpt-oss"
            )],
            selectedEndpointID: endpointID,
            selectedProviderID: providerID
        ))
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)

        await viewModel.updateLocalLLMEndpoint(
            id: endpointID,
            draft: LocalLLMEndpointDraft(
                displayName: "",
                isEnabled: false,
                endpoint: "http://localhost:2999/v1",
                modelsText: "gpt-oss",
                selectedModel: "gpt-oss",
                apiKey: ""
            ),
            apiKeyWasEdited: false
        )

        XCTAssertNil(store.load().selectedProviderID)
    }

    func testLocalLLMEndpointCreateReportsPersistenceFailure() async throws {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let store = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: InMemoryLocalLLMSecretStore(),
            configurationWriter: { _, _ in false }
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        viewModel.localLLMEndpoint = "http://localhost:2999/v1"
        viewModel.localLLMModelsText = "gpt-oss"

        await viewModel.createLocalLLMEndpoint()

        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
        XCTAssertEqual(store.load(), .disabled)
    }

}

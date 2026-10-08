import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SettingsCredentialViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testCatalogueSelectionCreatesOnlySelectedCanonicalClosure() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            ApiFixtureLoader.data("native.my.api-keys.create"), 201
        )
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Coding agent"
        await model.loadCredentialSettings()
        XCTAssertFalse(model.canCreateApiKey)

        model.setApiKeyScope("data:write", selected: true)
        XCTAssertEqual(model.apiKeyScopeSelection.permissions, ["data:read", "data:write"])
        XCTAssertTrue(model.canCreateApiKey)
        await model.createApiKey()

        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.method == "POST" })
        let body = try XCTUnwrap(request.body).data(using: .utf8)
        let json = try XCTUnwrap(JSONSerialization.jsonObject(with: XCTUnwrap(body)) as? [String: Any])
        XCTAssertEqual(json["permissions"] as? [String], ["data:read", "data:write"])
        XCTAssertEqual(json["type"] as? String, "mcp")
        XCTAssertEqual(model.latestRawAPIKey, "fixture-raw-api-key")
        XCTAssertEqual(model.apiKeys.count, 1)
        XCTAssertTrue(model.apiKeyScopeSelection.permissions.isEmpty)
        model.apiKeyType = .rss
        XCTAssertEqual(model.apiKeyScopes.map(\.scope), ["feed:read"])
    }

    func testInvalidCreationDoesNotSendAndFailureRetainsSelectionAndDraft() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Keep draft"
        await model.loadCredentialSettings()
        await model.createApiKey()
        XCTAssertFalse(CannedFeedURLProtocol.capturedMethods.contains("POST"))
        model.setApiKeyScope("data:write", selected: true)
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (Data(#"{"error":"denied"}"#.utf8), 403)
        await model.createApiKey()
        XCTAssertEqual(model.apiKeyLabel, "Keep draft")
        XCTAssertEqual(model.apiKeyScopeSelection.permissions, ["data:read", "data:write"])
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertTrue(model.apiKeys.isEmpty)
        guard case .error = model.state else { return XCTFail("Creation failure must be visible") }
        XCTAssertNotNil(model.statusMessage)
    }

    func testCatalogueAndAuthorizationFailuresRetainTruthfulGrantState() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (Data("{".utf8), 200)
        await model.loadCredentialSettings()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        XCTAssertFalse(model.canCreateApiKey)
        guard case .error = model.credentialState else { return XCTFail("Invalid catalogue must fail") }
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (Data(#"{"error":"unauthorized"}"#.utf8), 401)
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            ApiFixtureLoader.data("native.my.api-keys.create"),
            201
        )
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        await model.loadCredentialSettings()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        XCTAssertFalse(model.hasNoOAuthGrants)
        guard case .loaded = model.credentialState else { return XCTFail("Usable catalogue must remain loaded") }
        guard case .error = model.oauthGrantState else { return XCTFail("Grant authorization failure must be visible") }
        model.setApiKeyScope("data:write", selected: true)
        XCTAssertTrue(model.canCreateApiKey)
        await model.createApiKey()
        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.contains { $0.method == "POST" })
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (SettingsCredentialsTestData.grants(["two"]), 200)
        await model.loadCredentialSettings()
        guard case .loaded = model.oauthGrantState else { return XCTFail("Grant retry must recover") }
        XCTAssertEqual(model.oauthGrants.map(\.id), ["two"])
    }

    func testOAuthGrantsBecomeAvailableWhileScopeCatalogIsStillLoading() async throws {
        seedCredentials()
        let scopePath = "/api/v1/scopes"
        let scopeResponseSuspended = expectation(description: "Scope catalog response is held")
        CannedFeedURLProtocol.suspendResponse(path: scopePath) {
            scopeResponseSuspended.fulfill()
        }
        let model = try SettingsViewModel(client: makeClient())
        let load = Task { await model.loadCredentialSettings() }
        defer { CannedFeedURLProtocol.releaseResponse(path: scopePath) }

        await fulfillment(of: [scopeResponseSuspended], timeout: 1)
        await assertEventuallyLoaded(\.oauthGrantState, on: model)

        guard case .loading = model.credentialState else {
            CannedFeedURLProtocol.releaseResponse(path: scopePath)
            await load.value
            return XCTFail("Held scope catalog should not block the completed grant request")
        }
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])

        CannedFeedURLProtocol.releaseResponse(path: scopePath)
        await load.value
        guard case .loaded = model.credentialState else { return XCTFail("Released scope catalog should load") }
    }

    func testScopeCatalogBecomesAvailableWhileOAuthGrantsAreStillLoading() async throws {
        seedCredentials()
        let grantsPath = "/api/v1/my/oauth-grants"
        let grantsResponseSuspended = expectation(description: "OAuth grants response is held")
        CannedFeedURLProtocol.suspendResponse(path: grantsPath) {
            grantsResponseSuspended.fulfill()
        }
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        let load = Task { await model.loadCredentialSettings() }
        defer { CannedFeedURLProtocol.releaseResponse(path: grantsPath) }

        await fulfillment(of: [grantsResponseSuspended], timeout: 1)
        await assertEventuallyLoaded(\.credentialState, on: model)

        guard case .loading = model.oauthGrantState else {
            CannedFeedURLProtocol.releaseResponse(path: grantsPath)
            await load.value
            return XCTFail("Held grants should not block the completed scope-catalog request")
        }
        XCTAssertTrue(model.apiKeyScopes.contains { $0.scope == "data:write" })

        CannedFeedURLProtocol.releaseResponse(path: grantsPath)
        await load.value
        guard case .loaded = model.oauthGrantState else { return XCTFail("Released grants response should load") }
    }

    func testGrantPaginationRetriesOpaqueCursorAndDeduplicatesPages() async throws {
        seedCredentials(hasMore: true)
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (Data(#"{"error":"denied"}"#.utf8), 403)
        await model.loadMoreOAuthGrants()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        XCTAssertNotNil(model.oauthGrantPagination.lastError)
        XCTAssertTrue(model.oauthGrantPagination.hasMore)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (
            SettingsCredentialsTestData.grants(["one", "two"], lastUsed: true), 200
        )
        await model.loadMoreOAuthGrants()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one", "two"])
        XCTAssertNil(model.oauthGrantPagination.lastError)
        XCTAssertFalse(model.oauthGrantPagination.hasMore)
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(
            URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems?
                .first { $0.name == "after" }?.value,
            "grant-next+/="
        )
    }

    func testRevokeFailureRetainsGrantAndSuccessRemovesOnlyOwnedGrant() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (
            SettingsCredentialsTestData.grants(["one", "two"]),
            200
        )
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        await model.revokeOAuthGrant(id: "not-visible")
        XCTAssertFalse(CannedFeedURLProtocol.capturedMethods.contains("DELETE"))
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants/one"] = (Data(#"{"error":"denied"}"#.utf8), 403)
        await model.revokeOAuthGrant(id: "one")
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one", "two"])
        XCTAssertNotNil(model.statusMessage)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants/one"] = (Data(), 204)
        await model.revokeOAuthGrant(id: "one")
        XCTAssertEqual(model.oauthGrants.map(\.id), ["two"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/my/oauth-grants/one")
        await model.loadCredentialSettings()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["two"], "Stale first page must not restore revoked access")
    }

    func testMissingGrantReconcilesButAuthorizationFailureRetainsTheRow() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants/one"] = (Data(), 403)
        await model.revokeOAuthGrant(id: "one")
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])

        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants/one"] = (Data(), 404)
        await model.revokeOAuthGrant(id: "one")
        XCTAssertTrue(model.oauthGrants.isEmpty)
        XCTAssertTrue(model.hasNoOAuthGrants)
    }

    func testRevokingLastVisibleGrantDoesNotClaimEmptyWhileAnotherPageExists() async throws {
        seedCredentials(hasMore: true)
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants/one"] = (Data(), 204)

        await model.revokeOAuthGrant(id: "one")

        XCTAssertTrue(model.oauthGrants.isEmpty)
        XCTAssertTrue(model.oauthGrantPagination.hasMore)
        XCTAssertFalse(model.hasNoOAuthGrants)
    }

    func testRevocationInvalidatesPendingGrantPage() async throws {
        seedCredentials(hasMore: true)
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        let path = "/api/v1/my/oauth-grants"
        CannedFeedURLProtocol.handlers[path] = (SettingsCredentialsTestData.grants(["one", "two"]), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let pending = Task { await model.loadMoreOAuthGrants() }
        _ = try await barrier.wait()
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants/one"] = (Data(), 204)
        await model.revokeOAuthGrant(id: "one")
        CannedFeedURLProtocol.releaseResponse(path: path)
        await pending.value
        XCTAssertTrue(model.oauthGrants.isEmpty)
    }

    func testRevocationDoesNotDiscardPendingScopeCatalogRetry() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (Data("{".utf8), 200)
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        await model.loadCredentialSettings()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        XCTAssertTrue(model.apiKeyScopes.isEmpty)

        let scopePath = "/api/v1/scopes"
        CannedFeedURLProtocol.handlers[scopePath] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.suspendResponse(path: scopePath)
        defer { CannedFeedURLProtocol.releaseResponse(path: scopePath) }
        let scopeBarrier = CannedFeedURLProtocol.requestBarrier(path: scopePath, method: "GET")
        let retry = Task { await model.loadCredentialSettings() }
        do {
            _ = try await scopeBarrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: scopePath)
            await retry.value
            throw error
        }

        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants/one"] = (Data(), 204)
        await model.revokeOAuthGrant(id: "one")
        XCTAssertTrue(model.oauthGrants.isEmpty)
        CannedFeedURLProtocol.releaseResponse(path: scopePath)
        await retry.value

        guard case .loaded = model.credentialState else { return XCTFail("Catalog retry must complete") }
        guard case .loaded = model.oauthGrantState else { return XCTFail("Grant revoke must remain loaded") }
        XCTAssertTrue(model.apiKeyScopes.contains { $0.scope == "data:write" })
        model.setApiKeyScope("data:write", selected: true)
        XCTAssertTrue(model.canCreateApiKey)
        XCTAssertTrue(model.oauthGrants.isEmpty, "Stale grant response must not restore revoked access")
    }

    private func seedCredentials(hasMore: Bool = false) {
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (
            SettingsCredentialsTestData.grants(["one"], hasMore: hasMore),
            200
        )
    }

    func testCancellationEndsCredentialLoadAndGrantPaginationWithoutPretendingSuccess() async throws {
        seedCredentials(hasMore: true)
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        let path = "/api/v1/my/oauth-grants"
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let loadBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let load = Task { await model.loadCredentialSettings() }
        _ = try await loadBarrier.wait()
        await assertEventuallyLoaded(\.credentialState, on: model)
        load.cancel()
        await load.value
        guard case .loaded = model.credentialState else {
            return XCTFail("Canceling grants must preserve the completed scope catalog")
        }
        guard case .idle = model.oauthGrantState else { return XCTFail("Canceled grant reload must end loading") }
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        let pageBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let page = Task { await model.loadMoreOAuthGrants() }
        _ = try await pageBarrier.wait()
        page.cancel()
        await page.value
        XCTAssertFalse(model.oauthGrantPagination.isLoading)
        XCTAssertNil(model.oauthGrantPagination.lastError)
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
    }

    func testInvalidPrerequisiteGraphIsVisibleAndCannotCreate() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (
            Data(
                #"{"scopes":[{"scope":"write","resource":"data","action":"write","audience":"user","requires":"missing","description_key":null,"surfaces":["api-key"]}]}"#
                    .utf8
            ), 200
        )
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        await model.loadCredentialSettings()
        XCTAssertFalse(model.canCreateApiKey)
        XCTAssertTrue(model.apiKeyScopes.isEmpty)
        guard case .error = model.credentialState else { return XCTFail("Invalid graph must be reported") }
    }

    private func assertEventuallyLoaded(
        _ keyPath: KeyPath<SettingsViewModel, LoadState>,
        on model: SettingsViewModel
    ) async {
        let loaded = expectation(description: "Settings state becomes loaded")
        func observe() {
            if case .loaded = model[keyPath: keyPath] {
                loaded.fulfill()
                return
            }
            withObservationTracking {
                _ = model[keyPath: keyPath]
            } onChange: {
                Task { @MainActor in observe() }
            }
        }
        observe()
        await fulfillment(of: [loaded], timeout: 1)
    }
}

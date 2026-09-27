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
        await model.loadCredentialSettings()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        guard case .error = model.credentialState else { return XCTFail("Authorization failure must be visible") }
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
        load.cancel()
        await load.value
        guard case .idle = model.credentialState else { return XCTFail("Canceled reload must end loading") }
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
}

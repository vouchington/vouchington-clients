import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
extension SettingsViewModelActionCoverageTests {
    func testHeldCreationCannotPublishOriginalOwnerSecretAfterIdentityChanges() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        model.apiKeyLabel = "Agent"
        model.setApiKeyScope("feed:read", selected: true)
        XCTAssertTrue(model.canCreateApiKey)

        let keyPath = "/api/v1/my/api-keys"
        let identityPath = "/api/v1/my/identity"
        CannedFeedURLProtocol.handlers[keyPath] = (ApiFixtureLoader.data("native.my.api-keys.create"), 201)
        CannedFeedURLProtocol.suspendResponse(path: keyPath)
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: keyPath)
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
        }

        let createBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "POST")
        let creation = Task { await model.createApiKey() }
        _ = try await createBarrier.wait()
        CannedFeedURLProtocol.handlers[identityPath] = (
            PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"), 200
        )
        let identityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let reload = Task { await model.load() }
        _ = try await identityBarrier.wait()

        CannedFeedURLProtocol.releaseResponse(path: keyPath)
        await creation.value
        XCTAssertNil(model.latestRawAPIKey, "A secret must stay hidden while identity is unconfirmed")
        CannedFeedURLProtocol.handlers[keyPath] = (emptyApiKeyPage(), 200)
        seedSecondOwnerDataRequest()
        CannedFeedURLProtocol.releaseResponse(path: identityPath)
        await reload.value

        XCTAssertEqual(model.identity?.id, "user-2")
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertTrue(model.apiKeys.isEmpty, "B's key page: \(model.apiKeys.map(\.id)); state: \(model.state)")
        XCTAssertEqual(model.apiKeyLabel, "Agent")
        XCTAssertNil(model.statusMessage)
    }

    func testHeldCreationRevealsSecretOnlyAfterSameOwnerIsReconfirmed() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        model.apiKeyLabel = "Agent"
        model.setApiKeyScope("feed:read", selected: true)

        let keyPath = "/api/v1/my/api-keys"
        let identityPath = "/api/v1/my/identity"
        CannedFeedURLProtocol.handlers[keyPath] = (ApiFixtureLoader.data("native.my.api-keys.create"), 201)
        CannedFeedURLProtocol.suspendResponse(path: keyPath)
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: keyPath)
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
        }

        let createBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "POST")
        let creation = Task { await model.createApiKey() }
        _ = try await createBarrier.wait()
        let identityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let reload = Task { await model.load() }
        _ = try await identityBarrier.wait()

        CannedFeedURLProtocol.releaseResponse(path: keyPath)
        await creation.value
        XCTAssertNil(model.latestRawAPIKey)
        CannedFeedURLProtocol.handlers[keyPath] = (emptyApiKeyPage(), 200)
        CannedFeedURLProtocol.releaseResponse(path: identityPath)
        await reload.value

        XCTAssertEqual(model.identity?.id, "user-1")
        XCTAssertEqual(model.latestRawAPIKey, "fixture-raw-api-key")
        XCTAssertEqual(model.apiKeys.first?.id, "00000000-0000-7000-8000-000000000701")
        XCTAssertEqual(model.apiKeyLabel, "")
    }

    func testHeldCreationErrorCannotReplaceNewOwnersReloadState() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        model.apiKeyLabel = "Agent"
        model.setApiKeyScope("feed:read", selected: true)

        let keyPath = "/api/v1/my/api-keys"
        let identityPath = "/api/v1/my/identity"
        CannedFeedURLProtocol.handlers[keyPath] = (Data(#"{"error":"denied"}"#.utf8), 403)
        CannedFeedURLProtocol.suspendResponse(path: keyPath)
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: keyPath)
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
        }

        let createBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "POST")
        let creation = Task { await model.createApiKey() }
        _ = try await createBarrier.wait()
        CannedFeedURLProtocol.handlers[identityPath] = (
            PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"), 200
        )
        let identityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let reload = Task { await model.load() }
        _ = try await identityBarrier.wait()

        CannedFeedURLProtocol.releaseResponse(path: keyPath)
        await creation.value
        XCTAssertNil(model.statusMessage)
        CannedFeedURLProtocol.handlers[keyPath] = (emptyApiKeyPage(), 200)
        seedSecondOwnerDataRequest()
        CannedFeedURLProtocol.releaseResponse(path: identityPath)
        await reload.value

        XCTAssertEqual(model.identity?.id, "user-2")
        guard case .loaded = model.state else {
            return XCTFail("The new owner's reload must remain loaded: \(model.state)")
        }
        XCTAssertNil(model.statusMessage)
        XCTAssertNil(model.latestRawAPIKey)
    }

    func testOlderHeldCreationCannotReplaceNewerRotationSecret() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        model.apiKeyLabel = "Agent"
        model.setApiKeyScope("feed:read", selected: true)

        let keyPath = "/api/v1/my/api-keys"
        let rotatePath = "/api/v1/my/api-keys/key-1/rotate"
        let keyPage = try XCTUnwrap(CannedFeedURLProtocol.handlers[keyPath])
        CannedFeedURLProtocol.handlers[keyPath] = (ApiFixtureLoader.data("native.my.api-keys.create"), 201)
        CannedFeedURLProtocol.suspendResponse(path: keyPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: keyPath) }

        let creationBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "POST")
        let creation = Task { await model.createApiKey() }
        _ = try await creationBarrier.wait()

        CannedFeedURLProtocol.handlers[keyPath] = keyPage
        CannedFeedURLProtocol.handlers[rotatePath] = (
            ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
        )
        let rotationPageBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "GET")
        let rotation = Task { await model.rotateApiKey(id: "key-1") }
        _ = try await rotationPageBarrier.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: keyPath)
        await rotation.value
        XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key")

        CannedFeedURLProtocol.releaseOldestResponse(path: keyPath)
        await creation.value
        XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key")
        XCTAssertEqual(model.apiKeyLabel, "Agent", "The older completion cannot clear the newer draft")
    }

    func testOldOwnersHeldUnauthorizedCreationCannotInvalidateNewOwner() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        model.apiKeyLabel = "Agent"
        model.setApiKeyScope("feed:read", selected: true)

        let keyPath = "/api/v1/my/api-keys"
        CannedFeedURLProtocol.handlers[keyPath] = (Data(#"{"error":"signed out"}"#.utf8), 401)
        CannedFeedURLProtocol.suspendResponse(path: keyPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: keyPath) }
        let creationBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "POST")
        let creation = Task { await model.createApiKey() }
        _ = try await creationBarrier.wait()

        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"), 200
        )
        CannedFeedURLProtocol.handlers[keyPath] = (emptyApiKeyPage(), 200)
        seedSecondOwnerDataRequest()
        let keyPageBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "GET")
        let reload = Task { await model.load() }
        _ = try await keyPageBarrier.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: keyPath)
        await reload.value
        XCTAssertEqual(model.identity?.id, "user-2")
        XCTAssertTrue(model.apiKeyRotationOwnerState.identityConfirmed)

        CannedFeedURLProtocol.releaseOldestResponse(path: keyPath)
        await creation.value
        XCTAssertEqual(model.identity?.id, "user-2")
        XCTAssertTrue(model.apiKeyRotationOwnerState.identityConfirmed)
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertNil(model.statusMessage)
    }

    func testCurrentUnauthorizedCreationDuringIdentityReloadDoesNotLeavePermanentLoading() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        model.apiKeyLabel = "Agent"
        model.setApiKeyScope("feed:read", selected: true)

        let keyPath = "/api/v1/my/api-keys"
        let identityPath = "/api/v1/my/identity"
        CannedFeedURLProtocol.handlers[keyPath] = (Data(#"{"error":"signed out"}"#.utf8), 401)
        CannedFeedURLProtocol.suspendResponse(path: keyPath)
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: keyPath)
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
        }

        let creationBarrier = CannedFeedURLProtocol.requestBarrier(path: keyPath, method: "POST")
        let creation = Task { await model.createApiKey() }
        _ = try await creationBarrier.wait()
        let identityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let reload = Task { await model.load() }
        _ = try await identityBarrier.wait()

        CannedFeedURLProtocol.releaseResponse(path: keyPath)
        await creation.value
        XCTAssertNil(model.latestRawAPIKey)
        CannedFeedURLProtocol.releaseResponse(path: identityPath)
        await reload.value

        guard case .error(.unauthorized) = model.state else {
            return XCTFail("The current credential failure must end the loading state")
        }
        XCTAssertFalse(model.apiKeyRotationOwnerState.identityConfirmed)
        XCTAssertNil(model.latestRawAPIKey)
    }

    private func emptyApiKeyPage() -> Data {
        Data(#"{"results":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#.utf8)
    }

    private func seedSecondOwnerDataRequest() {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-2/data-request"] = (
            Data(#"{"error":"not found"}"#.utf8), 404
        )
    }
}

import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
extension SettingsViewModelActionCoverageTests {
    func testRotationSecretWaitsForLatestSameOwnerAcrossOverlappingAndTransientLoads() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()

        let identityPath = "/api/v1/my/identity"
        let rotationPath = "/api/v1/my/api-keys/key-1/rotate"
        let sameOwner = PrivateUserTestFixture.identityEnvelope()
        CannedFeedURLProtocol.queuedHandlers[identityPath] = [
            (sameOwner, 200, 0),
            (Data(#"{"error":"temporary"}"#.utf8), 503, 0),
            (sameOwner, 200, 0)
        ]
        CannedFeedURLProtocol.handlers[rotationPath] = (
            ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        CannedFeedURLProtocol.suspendResponse(path: rotationPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
            CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        }

        let rotationBarrier = CannedFeedURLProtocol.requestBarrier(path: rotationPath, method: "POST")
        let rotation = Task { await model.rotateApiKey(id: "key-1") }
        _ = try await rotationBarrier.wait()

        let staleIdentityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let staleLoad = Task { await model.load() }
        _ = try await staleIdentityBarrier.wait()
        let latestIdentityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let latestLoad = Task { await model.load() }
        _ = try await latestIdentityBarrier.wait()

        CannedFeedURLProtocol.releaseOldestResponse(path: identityPath)
        await staleLoad.value
        XCTAssertNil(model.latestRawAPIKey)

        CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        await rotation.value
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertTrue(model.apiKeyRotationInFlight.isEmpty)
        XCTAssertTrue(model.apiKeySecretOperationInFlight)
        model.dismissRawApiKey()
        XCTAssertTrue(
            model.apiKeySecretOperationInFlight,
            "Dismiss without a visible secret cannot drop the held rotation"
        )

        CannedFeedURLProtocol.releaseOldestResponse(path: identityPath)
        await latestLoad.value
        XCTAssertNil(model.latestRawAPIKey)

        let retryIdentityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let retryLoad = Task { await model.load() }
        _ = try await retryIdentityBarrier.wait()
        CannedFeedURLProtocol.releaseOldestResponse(path: identityPath)
        await retryLoad.value

        XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key")
        XCTAssertNotNil(model.statusMessage)
    }

    func testLateRotationCannotRestoreOriginalOwnerSecretAfterOwnerChangesAndReturns() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()

        let identityPath = "/api/v1/my/identity"
        let rotationPath = "/api/v1/my/api-keys/key-1/rotate"
        CannedFeedURLProtocol.queuedHandlers[identityPath] = [
            (PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"), 200, 0),
            (PrivateUserTestFixture.identityEnvelope(), 200, 0),
            (PrivateUserTestFixture.identityEnvelope(), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[rotationPath] = (
            ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        CannedFeedURLProtocol.suspendResponse(path: rotationPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
            CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        }

        let rotationBarrier = CannedFeedURLProtocol.requestBarrier(path: rotationPath, method: "POST")
        let rotation = Task { await model.rotateApiKey(id: "key-1") }
        _ = try await rotationBarrier.wait()

        let otherOwnerBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let otherOwnerLoad = Task { await model.load() }
        _ = try await otherOwnerBarrier.wait()
        CannedFeedURLProtocol.releaseOldestResponse(path: identityPath)
        await otherOwnerLoad.value
        XCTAssertEqual(model.identity?.id, "user-2")

        let originalOwnerBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let originalOwnerLoad = Task { await model.load() }
        _ = try await originalOwnerBarrier.wait()
        CannedFeedURLProtocol.releaseOldestResponse(path: identityPath)
        await originalOwnerLoad.value
        XCTAssertEqual(model.identity?.id, "user-1")

        let unknownOwnerBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let unknownOwnerLoad = Task { await model.load() }
        _ = try await unknownOwnerBarrier.wait()
        CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        await rotation.value
        XCTAssertNil(model.latestRawAPIKey)
        CannedFeedURLProtocol.releaseOldestResponse(path: identityPath)
        await unknownOwnerLoad.value
        XCTAssertEqual(model.identity?.id, "user-1")
        XCTAssertNil(model.latestRawAPIKey)
    }

    func testRotationSecretIsDiscardedWhenRefreshConfirmsAnotherOwner() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()

        let identityPath = "/api/v1/my/identity"
        let rotationPath = "/api/v1/my/api-keys/key-1/rotate"
        CannedFeedURLProtocol.queuedHandlers[identityPath] = [
            (PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[rotationPath] = (
            ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        CannedFeedURLProtocol.suspendResponse(path: rotationPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
            CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        }

        let rotationBarrier = CannedFeedURLProtocol.requestBarrier(path: rotationPath, method: "POST")
        let rotation = Task { await model.rotateApiKey(id: "key-1") }
        _ = try await rotationBarrier.wait()
        let identityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let load = Task { await model.load() }
        _ = try await identityBarrier.wait()

        CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        await rotation.value
        XCTAssertNil(model.latestRawAPIKey)

        CannedFeedURLProtocol.releaseResponse(path: identityPath)
        await load.value
        XCTAssertEqual(model.identity?.id, "user-2")
        XCTAssertNil(model.latestRawAPIKey)
    }

    func testRotationSecretIsDiscardedWhenRefreshConfirmsSignedOutSession() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()

        let identityPath = "/api/v1/my/identity"
        let rotationPath = "/api/v1/my/api-keys/key-1/rotate"
        CannedFeedURLProtocol.queuedHandlers[identityPath] = [
            (Data(#"{"error":"signed out"}"#.utf8), 401, 0)
        ]
        CannedFeedURLProtocol.handlers[rotationPath] = (
            ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        CannedFeedURLProtocol.suspendResponse(path: rotationPath)
        defer {
            CannedFeedURLProtocol.releaseResponse(path: identityPath)
            CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        }

        let rotationBarrier = CannedFeedURLProtocol.requestBarrier(path: rotationPath, method: "POST")
        let rotation = Task { await model.rotateApiKey(id: "key-1") }
        _ = try await rotationBarrier.wait()
        let identityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let load = Task { await model.load() }
        _ = try await identityBarrier.wait()

        CannedFeedURLProtocol.releaseResponse(path: rotationPath)
        await rotation.value
        XCTAssertNil(model.latestRawAPIKey)

        CannedFeedURLProtocol.releaseResponse(path: identityPath)
        await load.value
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertNil(model.identity)
        await model.rotateApiKey(id: "key-1")
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedRequests.filter {
                $0.method == "POST" && $0.url.path == rotationPath
            }.count,
            1
        )
    }

}

import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
extension SettingsViewModelActionCoverageTests {
    func testLateLogoutTransitionForPreviousOwnerCannotClearCurrentOwnerSecret() async throws {
        for transition in ["current-session", "all-sessions", "account"] {
            seedSettingsResponses()
            let rotationPath = "/api/v1/my/api-keys/key-1/rotate"
            CannedFeedURLProtocol.handlers[rotationPath] = (
                ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
            )
            let path: String
            let method: String
            let body: Data
            switch transition {
            case "current-session":
                path = "/api/v1/auth/sessions/session-1"
                method = "DELETE"
                body = Data("{}".utf8)
            case "all-sessions":
                path = "/api/v1/auth/sessions/revocations"
                method = "POST"
                body = Data("{}".utf8)
            default:
                path = "/api/v1/users/user-1"
                method = "DELETE"
                body = Data(#"{"logout":true}"#.utf8)
            }
            CannedFeedURLProtocol.handlers[path] = (body, 200)
            CannedFeedURLProtocol.suspendResponse(path: path)
            defer { CannedFeedURLProtocol.releaseResponse(path: path) }

            let didLogout = LogoutExpectation()
            let model = try SettingsViewModel(client: makeClient()) { didLogout.value = true }
            await model.load()
            if transition == "account" { model.deleteConfirmation = "delete my account" }
            let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: method)
            let action = Task {
                switch transition {
                case "current-session": await model.revokeSession(id: "session-1")
                case "all-sessions": await model.revokeAllSessions()
                default: await model.deleteAccount()
                }
            }
            _ = try await barrier.wait()

            CannedFeedURLProtocol.queuedHandlers["/api/v1/my/identity"] = [
                (PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"), 200, 0)
            ]
            await model.load()
            XCTAssertEqual(model.identity?.id, "user-2")
            await model.rotateApiKey(id: "key-1")
            XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.contains {
                $0.method == "POST" && $0.url.path == rotationPath
            }, transition)
            XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key")

            CannedFeedURLProtocol.releaseResponse(path: path)
            await action.value
            XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key", transition)
            XCTAssertFalse(didLogout.value, transition)
        }
    }

    func testStaleRotationUnauthorizedCannotClearNewOwnerSecret() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()

        let identityPath = "/api/v1/my/identity"
        let rotationPath = "/api/v1/my/api-keys/key-1/rotate"
        CannedFeedURLProtocol.queuedHandlers[identityPath] = [
            (PrivateUserTestFixture.identityEnvelope(id: "user-2", username: "bob"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers[rotationPath] = [
            (Data(#"{"error":"signed out"}"#.utf8), 401, 0),
            (ApiFixtureLoader.data("native.my.api-keys.rotate"), 201, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: rotationPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: rotationPath) }

        let staleBarrier = CannedFeedURLProtocol.requestBarrier(path: rotationPath, method: "POST")
        let staleRotation = Task { await model.rotateApiKey(id: "key-1") }
        _ = try await staleBarrier.wait()
        await model.load()
        XCTAssertEqual(model.identity?.id, "user-2")

        let currentBarrier = CannedFeedURLProtocol.requestBarrier(path: rotationPath, method: "POST")
        let currentRotation = Task { await model.rotateApiKey(id: "key-1") }
        _ = try await currentBarrier.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: rotationPath)
        await currentRotation.value
        XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key")

        CannedFeedURLProtocol.releaseOldestResponse(path: rotationPath)
        await staleRotation.value
        XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key")
    }

    func testHeldIdentityCannotReauthorizeAfterCurrentSessionLogout() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()

        let identityPath = "/api/v1/my/identity"
        CannedFeedURLProtocol.queuedHandlers[identityPath] = [
            (PrivateUserTestFixture.identityEnvelope(), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: identityPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: identityPath) }
        let identityBarrier = CannedFeedURLProtocol.requestBarrier(path: identityPath, method: "GET")
        let load = Task { await model.load() }
        _ = try await identityBarrier.wait()

        await model.revokeSession(id: "session-1")
        CannedFeedURLProtocol.releaseResponse(path: identityPath)
        await load.value

        XCTAssertNil(model.identity)
        await model.rotateApiKey(id: "key-1")
        XCTAssertFalse(CannedFeedURLProtocol.capturedRequests.contains {
            $0.method == "POST" && $0.url.path == "/api/v1/my/api-keys/key-1/rotate"
        })
    }
}

@MainActor
private final class LogoutExpectation {
    var value = false
}

import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
extension SettingsViewModelActionCoverageTests {
    func testApiKeyLifetimeDefaultsAndAdministratorCannotChooseUnlimited() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(roles: ["administrator"]), 200
        )
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        XCTAssertTrue(model.isApiKeyAdministrator)
        XCTAssertEqual(model.apiKeyLifetimeDays, 30)
        model.setApiKeyLifetimeDays(nil)
        XCTAssertEqual(model.apiKeyLifetimeDays, 30)
        model.setApiKeyLifetimeDays(365)
        XCTAssertEqual(model.apiKeyLifetimeDays, 30)

        model.apiKeyLabel = "Admin reader"
        model.setApiKeyScope("feed:read", selected: true)
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            ApiFixtureLoader.data("native.my.api-keys.create"), 201
        )
        await model.createApiKey()
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.last { $0.method == "POST" })
        let body = try XCTUnwrap(request.body).data(using: .utf8)
        let json = try XCTUnwrap(JSONSerialization.jsonObject(with: XCTUnwrap(body)) as? [String: Any])
        XCTAssertEqual(json["lifetime_days"] as? Int, 30)
        XCTAssertNotNil(model.latestRawAPIKey)
        model.dismissRawApiKey()
        XCTAssertNil(model.latestRawAPIKey)
    }

    func testApiKeyRotationShowsReplacementOnceAndRejectsMissingOrInactiveKeys() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys/key-1/rotate"] = (
            Data(#"{"error":"missing"}"#.utf8), 404
        )
        await model.rotateApiKey(id: "key-1")
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertEqual(model.apiKeys.map(\.id), ["key-1"])
        XCTAssertEqual(uiEnglish(model.statusMessage), "This key is no longer available. Refresh your keys.")

        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys/key-1/rotate"] = (
            Data(#"{"error":"inactive"}"#.utf8), 409
        )
        await model.rotateApiKey(id: "key-1")
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertEqual(uiEnglish(model.statusMessage), "This key cannot be rotated. Refresh its state or revoke it.")

        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys/key-1/rotate"] = (
            ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (Data(#"{"error":"unavailable"}"#.utf8), 503)
        await model.rotateApiKey(id: "key-1")
        XCTAssertEqual(model.latestRawAPIKey, "fixture-rotated-api-key")
        XCTAssertEqual(model.apiKeys.first?.id, "00000000-0000-7000-8000-000000000702")
        model.dismissRawApiKey()
        XCTAssertNil(model.latestRawAPIKey)
    }

    func testLateRotationListCannotReplaceNewerSettingsLoad() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        let path = "/api/v1/my/api-keys"
        let oldPage = try XCTUnwrap(CannedFeedURLProtocol.handlers[path]).0
        let freshPage = Data(String(decoding: oldPage, as: UTF8.self)
            .replacingOccurrences(of: "key-1", with: "fresh-key").utf8)
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys/key-1/rotate"] = (
            ApiFixtureLoader.data("native.my.api-keys.rotate"), 201
        )
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let firstBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let rotation = Task { await model.rotateApiKey(id: "key-1") }
        do {
            _ = try await firstBarrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await rotation.value
            throw error
        }

        CannedFeedURLProtocol.handlers[path] = (freshPage, 200)
        let secondBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let reload = Task { await model.load() }
        do {
            _ = try await secondBarrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await rotation.value
            await reload.value
            throw error
        }
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await reload.value
        CannedFeedURLProtocol.releaseOldestResponse(path: path)
        await rotation.value
        XCTAssertEqual(model.apiKeys.map(\.id), ["fresh-key"])
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertNil(model.statusMessage)
    }

    func testLateRotationPostCannotShowPreviousOwnerSecretAfterReload() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        let path = "/api/v1/my/api-keys/key-1/rotate"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("native.my.api-keys.rotate"), 201)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        let rotation = Task { await model.rotateApiKey(id: "key-1") }
        do {
            _ = try await barrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await rotation.value
            throw error
        }
        await model.load()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await rotation.value
        XCTAssertEqual(model.apiKeys.map(\.id), ["key-1"])
        XCTAssertNil(model.latestRawAPIKey)
        XCTAssertNil(model.statusMessage)
    }

}

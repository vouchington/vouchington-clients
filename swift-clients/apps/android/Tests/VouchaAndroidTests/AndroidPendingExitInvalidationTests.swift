import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

@MainActor
final class AndroidPendingExitInvalidationTests: XCTestCase {
    func test401DuringHeldFailedSignOutSaveRetainsTurnForSameOwnerOnly() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let account = try await signedInAccount()
        defer { HeldAuthenticationURLProtocol.removeRequest(host: account.config.baseURL.host ?? "") }
        let userID = try XCTUnwrap(account.session.currentUserId)
        let ownerID = AndroidTranscriptTestFactory.pendingOwnerKey(userID, client: fixture.client)
        let store = AndroidPendingTurnStoreTestDouble()
        store.failSave = true
        let model = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, response: "Saved answer"), pendingStore: store
        )
        model.modelState = .available
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: userID)
        model.draft = "Saved question"
        await model.sendOnDevice()
        let pending = try XCTUnwrap(model.pendingTurnForSelectedConversation)

        store.failSave = false
        store.holdSave = true
        let preflight = Task { await model.prepareForAccountExit() }
        await store.waitForSaveEntry()
        try await forceIdentity401(account)
        let invalidation = Task { await model.bindAccount(client: nil, userID: account.session.currentUserId) }
        await Task.yield()
        XCTAssertNil(model.currentAccountUserID)
        store.failSaveAfterHold = true
        store.releaseSave()
        let exitAllowed = await preflight.value
        XCTAssertFalse(exitAllowed)
        await invalidation.value

        XCTAssertEqual(model.lockedPendingTurnsByOwner[ownerID], [pending])
        XCTAssertNil(store.records[ownerID]?[pending.conversationID])
        let postPath = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)

        store.failSaveAfterHold = false
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: "other-owner")
        XCTAssertTrue(model.pendingTurns.isEmpty)
        await model.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: userID)
        XCTAssertEqual(model.pendingTurnForSelectedConversation, pending)
        XCTAssertEqual(model.pendingTurnForSelectedConversation?.requestBody, pending.requestBody)
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)
    }

    private func signedInAccount() async throws -> AndroidAccount {
        let host = "identity-\(UUID().uuidString.lowercased()).example.test"
        let baseURL = try XCTUnwrap(URL(string: "https://\(host)"))
        let account = AndroidAccount(
            config: AppConfig(baseURL: baseURL),
            cookieStorage: AndroidSessionCookieStorage(secrets: MemorySecrets(), baseURL: baseURL),
            protocolClasses: [HeldAuthenticationURLProtocol.self]
        )
        let path = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [path])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(path, root: root))
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/my/identity")
        let refresh = Task { await account.session.refresh() }
        await fulfillment(of: [started], timeout: 3)
        XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 200, body: identity))
        let signedIn = await refresh.value
        XCTAssertTrue(signedIn)
        return account
    }

    private func forceIdentity401(_ account: AndroidAccount) async throws {
        let host = try XCTUnwrap(account.config.baseURL.host)
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/my/identity")
        let refresh = Task { await account.session.refresh() }
        await fulfillment(of: [started], timeout: 3)
        XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 401, body: Data("{}".utf8)))
        let refreshed = await refresh.value
        XCTAssertFalse(refreshed)
        XCTAssertNil(account.session.currentUserId)
    }
}

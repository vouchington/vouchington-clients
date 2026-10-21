import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

@MainActor
final class PendingTurnInvalidationTests: XCTestCase {
    func testIdentity401RetainsFailedSaveForSameOwnerAndExplicitRetry() async throws {
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
        let postPath = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)

        let host = try XCTUnwrap(account.config.baseURL.host)
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/my/identity")
        let refresh = Task { await account.session.refresh() }
        await fulfillment(of: [started], timeout: 3)
        XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 401, body: Data("{}".utf8)))
        let refreshed = await refresh.value
        XCTAssertFalse(refreshed)
        XCTAssertNil(account.session.currentUserId)
        await model.bindAccount(client: nil, userID: account.session.currentUserId)

        assertForcedExitRetained(pending, model: model, store: store, ownerID: ownerID, fixture: fixture)

        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: "other-owner")
        XCTAssertTrue(model.pendingTurns.isEmpty)
        await model.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: userID)
        let restored = try XCTUnwrap(model.pendingTurnForSelectedConversation)
        XCTAssertEqual(restored, pending)
        XCTAssertEqual(restored.requestBody, pending.requestBody)
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)

        store.failSave = false
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder, userText: pending.userText, assistantText: pending.assistantText
        )
        await model.retryPendingTurn()
        let posts = fixture.recorder.requestSnapshot(path: postPath)
        XCTAssertEqual(posts.count, 1)
        XCTAssertEqual(posts.first?.httpBody, pending.requestBody)
        XCTAssertNil(model.pendingTurnForSelectedConversation)
    }

    func testHeldForcedSaveCannotExposeOldTurnOrClearNewOwnerAcrossRoundTrip() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.failSave = true
        let model = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, response: "Saved answer"), pendingStore: store
        )
        model.modelState = .available
        await model.bindAccount(client: fixture.client, userID: "owner-a")
        model.draft = "Saved question"
        await model.sendOnDevice()
        let pendingA = try XCTUnwrap(model.pendingTurnForSelectedConversation)
        let ownerB = AndroidTranscriptTestFactory.pendingOwnerKey("owner-b", client: fixture.client)
        let pendingB = AndroidPendingChatTurn(
            conversationID: "other-conversation", userMessageID: "other-user", assistantMessageID: "other-assistant",
            userText: "Other question", assistantText: "Other answer",
            modelProvider: "android_aicore", modelName: nil
        )
        store.records[ownerB] = [pendingB.conversationID: pendingB]
        store.failSave = false
        store.holdSave = true
        let invalidation = Task { await model.bindAccount(client: nil, userID: nil) }
        await store.waitForSaveEntry()
        XCTAssertNil(model.currentAccountUserID)
        XCTAssertTrue(model.pendingTurns.isEmpty)

        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: "owner-b")
        XCTAssertEqual(model.unlistedPendingTurns, [pendingB])
        store.holdSave = false
        await model.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let rebindA = Task { await model.bindAccount(client: fixture.client, userID: "owner-a") }
        store.releaseSave()
        await invalidation.value
        await rebindA.value
        XCTAssertEqual(model.pendingTurnForSelectedConversation, pendingA)
        XCTAssertEqual(model.pendingTurnForSelectedConversation, pendingA)
        let postPath = "/api/v1/conversations/\(pendingA.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)
    }
}

private extension PendingTurnInvalidationTests {
    func assertForcedExitRetained(
        _ pending: AndroidPendingChatTurn, model: ViewModel, store: AndroidPendingTurnStoreTestDouble,
        ownerID: String, fixture: AndroidTranscriptTestClient
    ) {
        XCTAssertNil(model.currentAccountUserID)
        XCTAssertTrue(model.pendingTurns.isEmpty)
        XCTAssertEqual(store.saveAttempts.map(\.ownerID), [ownerID, ownerID])
        XCTAssertEqual(store.saveAttempts.map(\.body), [pending.requestBody, pending.requestBody])
        XCTAssertTrue(store.records[ownerID, default: [:]].isEmpty)
        let postPath = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)
    }

    func signedInAccount() async throws -> AndroidAccount {
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
        let refreshed = await refresh.value
        XCTAssertTrue(refreshed)
        return account
    }
}

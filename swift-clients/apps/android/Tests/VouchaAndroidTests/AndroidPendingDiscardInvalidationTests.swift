import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

@MainActor
final class AndroidPendingDiscardInvalidationTests: XCTestCase {
    func test401DuringHeldConfirmedDiscardDoesNotResaveDeletedTurn() async throws {
        try await runHeldDiscardScenario(fails: false)
    }

    func test401DuringHeldFailedDiscardRetainsOwnerLockedTurnForRetry() async throws {
        try await runHeldDiscardScenario(fails: true)
    }

    private func runHeldDiscardScenario(fails: Bool) async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let account = try await signedInAccount()
        defer { HeldAuthenticationURLProtocol.removeRequest(host: account.config.baseURL.host ?? "") }
        let userID = try XCTUnwrap(account.session.currentUserId)
        let ownerID = AndroidTranscriptTestFactory.pendingOwnerKey(userID, client: fixture.client)
        let ownerB = AndroidTranscriptTestFactory.pendingOwnerKey("other-owner", client: fixture.client)
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
        let confirmation = try XCTUnwrap(model.pendingTurnDiscardConfirmation())
        let pendingB = AndroidPendingChatTurn(
            conversationID: "other-conversation", userMessageID: "other-user", assistantMessageID: "other-assistant",
            userText: "Other question", assistantText: "Other answer",
            modelProvider: "android_aicore", modelName: nil
        )
        store.records[ownerB] = [pendingB.conversationID: pendingB]
        store.holdNextDiscard = true
        let discard = Task { await model.discardPendingTurns(after: confirmation) }
        await store.waitForDiscardEntry()

        try await forceIdentity401(account)
        let invalidation = Task { await model.bindAccount(client: nil, userID: account.session.currentUserId) }
        await Task.yield()
        XCTAssertNil(model.currentAccountUserID)
        XCTAssertEqual(model.lockedPendingTurnsByOwner[ownerID], [pending])
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: "other-owner")
        XCTAssertEqual(model.unlistedPendingTurns, [pendingB])

        store.failDiscardAfterPartialDelete = fails
        store.releaseDiscard()
        let discardedBeforeInvalidation = await discard.value
        XCTAssertFalse(discardedBeforeInvalidation)
        await invalidation.value
        let outcome = DiscardInvalidationOutcome(
            model: model, store: store, fixture: fixture,
            userID: userID, ownerID: ownerID, ownerB: ownerB,
            pending: pending, pendingB: pendingB
        )
        try await assertDiscardOutcome(outcome, fails: fails)
    }

    private func assertDiscardOutcome(_ context: DiscardInvalidationOutcome, fails: Bool) async throws {
        let model = context.model
        let store = context.store
        let fixture = context.fixture
        let pending = context.pending
        XCTAssertEqual(model.currentAccountUserID, "other-owner")
        XCTAssertEqual(store.records[context.ownerB]?[context.pendingB.conversationID], context.pendingB)
        XCTAssertEqual(store.saveAttempts.count, 1)
        let postPath = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)
        XCTAssertEqual(model.lockedPendingTurnsByOwner[context.ownerID], fails ? [pending] : nil)

        await model.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: context.userID)
        if fails {
            XCTAssertTrue(model.pendingStoreLoadFailed)
            XCTAssertEqual(model.lockedPendingTurnsByOwner[context.ownerID], [pending])
            store.failDiscardAfterPartialDelete = false
            let retry = try XCTUnwrap(model.pendingTurnDiscardConfirmation())
            let retriedDiscard = await model.discardPendingTurns(after: retry)
            XCTAssertTrue(retriedDiscard)
            XCTAssertNil(model.lockedPendingTurnsByOwner[context.ownerID])
        } else {
            XCTAssertNil(model.pendingTurnForSelectedConversation)
            XCTAssertNil(store.records[context.ownerID]?[pending.conversationID])
        }
        XCTAssertEqual(store.records[context.ownerB]?[context.pendingB.conversationID], context.pendingB)
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

@MainActor
private struct DiscardInvalidationOutcome {
    let model: ViewModel
    let store: AndroidPendingTurnStoreTestDouble
    let fixture: AndroidTranscriptTestClient
    let userID: String
    let ownerID: String
    let ownerB: String
    let pending: AndroidPendingChatTurn
    let pendingB: AndroidPendingChatTurn
}

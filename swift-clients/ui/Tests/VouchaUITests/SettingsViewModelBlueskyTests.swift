import Foundation
import ViewInspector
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsViewModelBlueskyTests: NativeRouteSurfaceViewModelTestCase {
    func testDisconnectSuccessClearsLinkedAccountAfterReload() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            linkedBlueskyIdentity(), 200
        )
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        XCTAssertEqual(viewModel.identity?.blueskyAccount?.did, "did:plc:alice")
        CannedFeedURLProtocol.handlers["/api/v1/auth/bluesky/link"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(), 200
        )

        await viewModel.disconnectBluesky()

        XCTAssertNil(viewModel.identity?.blueskyAccount)
        XCTAssertEqual(viewModel.blueskyLinkState, .idle)
        let disconnectIndex = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.lastIndex {
            $0.path == "/api/v1/auth/bluesky/link"
        })
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods[disconnectIndex], "DELETE")
    }

    func testDisconnectFailurePreservesLinkedAccountAndReportsError() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            linkedBlueskyIdentity(), 200
        )
        let viewModel = try SettingsViewModel(client: makeClient())
        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/auth/bluesky/link"] = (Data("{}".utf8), 500)

        await viewModel.disconnectBluesky()

        XCTAssertEqual(viewModel.identity?.blueskyAccount?.did, "did:plc:alice")
        XCTAssertEqual(
            viewModel.blueskyLinkState,
            .error(.message(.nativeSwiftSettingsBlueskyDisconnectFailed))
        )
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/auth/bluesky/link")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "DELETE")
    }

    func testBeginNativeLinkTrimsHandlePersistsFlowAndOpensHTTPSAuthorization() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/auth/bluesky/link"] = (
            ApiFixtureLoader.data("native.auth.bluesky.link.native"), 200
        )
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        let viewModel = try SettingsViewModel(client: makeClient(), blueskyLinkStore: store)
        viewModel.blueskyHandle = "  alice.bsky.social  "
        var openedURL: URL?

        await viewModel.beginBlueskyLink(store: store) { openedURL = $0 }

        XCTAssertEqual(openedURL, URL(string: "https://bsky.social/oauth/authorize"))
        XCTAssertEqual(try store.pending()?.flowId, "00000000-0000-7000-8000-00000000b501")
        XCTAssertEqual(viewModel.blueskyLinkState, .awaitingCallback)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.first??.contains(#""callback_mode":"native""#) == true)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.first??.contains(#""handle":"alice.bsky.social""#) == true)
    }

    func testAwaitingCallbackCanBeCancelledAndConnectReenabled() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: "verifier")
        let viewModel = SettingsViewModel(client: nil, blueskyLinkStore: store)

        XCTAssertEqual(viewModel.blueskyLinkState, .awaitingCallback)
        viewModel.cancelBlueskyLink()

        XCTAssertEqual(viewModel.blueskyLinkState, .cancelled)
        XCTAssertNil(try store.pending())
        XCTAssertTrue(viewModel.canBeginBlueskyLink)
    }

    func testExpiredPendingFlowRecoversToExpiredConnectableState() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: "verifier", now: Date(timeIntervalSince1970: 10))
        let viewModel = SettingsViewModel(
            client: nil,
            blueskyLinkStore: store,
            now: { Date(timeIntervalSince1970: 700) }
        )

        XCTAssertEqual(viewModel.blueskyLinkState, .expired)
        XCTAssertTrue(viewModel.canBeginBlueskyLink)
    }

    func testAwaitingFlowExpiresWhileExistingSettingsOwnerRemainsAlive() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: "verifier", now: Date(timeIntervalSince1970: 10))
        let viewModel = SettingsViewModel(
            client: nil,
            blueskyLinkStore: store,
            now: { Date(timeIntervalSince1970: 20) }
        )
        XCTAssertEqual(viewModel.blueskyLinkState, .awaitingCallback)

        viewModel.reconcileBlueskyLinkState(now: Date(timeIntervalSince1970: 700))

        XCTAssertEqual(viewModel.blueskyLinkState, .expired)
        XCTAssertTrue(viewModel.canBeginBlueskyLink)
    }

    func testExpiryTaskExpiresWhenSleepWakesAtExactDeadline() async throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: "verifier", now: Date(timeIntervalSince1970: 10))
        let clock = ScriptedBlueskyClock(seconds: [20, 20, 610])
        let viewModel = SettingsViewModel(
            client: nil,
            blueskyLinkStore: store,
            now: { clock.now() }
        )
        var delays: [TimeInterval] = []

        await viewModel.waitForBlueskyExpiry { delays.append($0) }

        XCTAssertEqual(delays, [590])
        XCTAssertEqual(viewModel.blueskyLinkState, .expired)
    }

    func testExpiryTaskResleepsWhenWallClockMovesBackward() async throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: "verifier", now: Date(timeIntervalSince1970: 10))
        let clock = ScriptedBlueskyClock(seconds: [20, 20, 5, 610])
        let viewModel = SettingsViewModel(
            client: nil,
            blueskyLinkStore: store,
            now: { clock.now() }
        )
        var delays: [TimeInterval] = []

        await viewModel.waitForBlueskyExpiry { delays.append($0) }

        XCTAssertEqual(delays, [590, 605])
        XCTAssertEqual(viewModel.blueskyLinkState, .expired)
    }

    func testFinalizingStateIsVisibleAndCannotBeCancelled() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: "verifier")
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=token"
        ))
        XCTAssertNotNil(try store.claimCallback(for: callback))
        let viewModel = SettingsViewModel(client: nil, blueskyLinkStore: store)
        let surface = SettingsSurface(viewModel: viewModel, fediverseEnabled: true)

        XCTAssertEqual(viewModel.blueskyLinkState, .finalizing)
        XCTAssertNoThrow(try surface.inspect().find(text: "Finalizing..."))
        XCTAssertThrowsError(try surface.inspect().find(button: "Cancel"))
        XCTAssertFalse(viewModel.canBeginBlueskyLink)
    }

    func testTransientFinalizationErrorCannotOverwritePersistedRetry() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: "verifier")
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=token"
        ))
        XCTAssertNotNil(try store.claimCallback(for: callback))
        store.recordResult(.finalizationFailure)

        let viewModel = SettingsViewModel(client: nil, blueskyLinkStore: store)

        guard case .error = viewModel.blueskyLinkState else {
            return XCTFail("Expected the transient finalization error to remain visible")
        }
        XCTAssertFalse(viewModel.canBeginBlueskyLink)
        XCTAssertEqual(try store.pending()?.completionToken, "token")
    }

    func testExistingSettingsOwnerConsumesSuccessAndReloadsIdentity() async throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(overrides: [
                "bluesky_account": ["did": "did:plc:alice", "handle": "alice.bsky.social"]
            ]), 200
        )
        let viewModel = try SettingsViewModel(client: makeClient(), blueskyLinkStore: store)

        store.recordResult(.success)
        for _ in 0 ..< 100 where viewModel.identity?.blueskyAccount == nil {
            await Task.yield()
        }

        XCTAssertEqual(viewModel.blueskyLinkState, .success)
        XCTAssertEqual(viewModel.identity?.blueskyAccount?.handle, "alice.bsky.social")
    }

    func testEmptyHandleDoesNotStartNetworkRequest() async throws {
        let viewModel = try SettingsViewModel(client: makeClient())
        viewModel.blueskyHandle = "  "
        await viewModel.beginBlueskyLink { _ in XCTFail("Should not open authorization") }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        guard case .error = viewModel.blueskyLinkState else {
            return XCTFail("Expected a localized validation error")
        }
    }

    private func linkedBlueskyIdentity() -> Data {
        PrivateUserTestFixture.identityEnvelope(overrides: [
            "bluesky_account": ["did": "did:plc:alice", "handle": "alice.bsky.social"]
        ])
    }
}

private final class ScriptedBlueskyClock: @unchecked Sendable {
    private let lock = NSLock()
    private var seconds: [TimeInterval]

    init(seconds: [TimeInterval]) {
        self.seconds = seconds
    }

    func now() -> Date {
        lock.withLock {
            Date(timeIntervalSince1970: seconds.removeFirst())
        }
    }
}

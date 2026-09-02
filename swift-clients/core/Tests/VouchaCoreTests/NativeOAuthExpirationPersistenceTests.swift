import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

@MainActor
final class NativeOAuthExpirationPersistenceTests: XCTestCase {
    func testExpirationWriteFailurePreservesClaimedAuthorizationForRetry() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture.store, now: fixture.initialNow)
        let claimed = try XCTUnwrap(try fixture.store.claimCallback(
            for: callbackURL(),
            now: fixture.initialNow
        ))
        fixture.coordinator.synchronizeFromStore()
        fixture.clock.now = fixture.initialNow.addingTimeInterval(301)
        fixture.persistence.failNextWrite = true

        fixture.coordinator.synchronizeFromStore()

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending, claimed)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .expired(provider: .github, purpose: .authenticate)
        )
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testFailedRecordDoesNotPublishOrReplacePendingState() async throws {
        await Task.yield()
        let fixture = try makeFixture()
        try seedPending(fixture.store, now: fixture.initialNow)
        fixture.persistence.failNextWrite = true
        var changeCount = 0
        let observer = NotificationCenter.default.addObserver(
            forName: .nativeOAuthAuthorizationDidChange,
            object: nil,
            queue: nil
        ) { _ in
            changeCount += 1
        }
        defer { NotificationCenter.default.removeObserver(observer) }

        XCTAssertThrowsError(try fixture.store.record(.connected(provider: .github)))

        XCTAssertEqual(fixture.store.pending(now: fixture.initialNow)?.flowId, "callback-flow")
        XCTAssertNil(fixture.store.result())
        XCTAssertEqual(changeCount, 0)

        XCTAssertNoThrow(try fixture.store.record(.connected(provider: .github)))
        XCTAssertEqual(fixture.store.result(), .connected(provider: .github))
        XCTAssertEqual(changeCount, 1)
    }

    func testColdStartExpirationWriteFailureExposesAuthorizationForCancellation() async throws {
        await Task.yield()
        let persistence = ExpirationOAuthSecureState()
        let fixture = try makeFixture(persistence: persistence) { store, now in
            XCTAssertTrue(store.save(
                flowId: "expired-flow",
                provider: .facebook,
                purpose: .connect,
                completionProofVerifier: "verifier",
                expiresAt: now.addingTimeInterval(-1),
                now: now.addingTimeInterval(-2)
            ))
            persistence.failNextWrite = true
        }

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending?.flowId, "expired-flow")
        XCTAssertTrue(fixture.coordinator.canCancelPendingAuthorization)

        fixture.coordinator.cancelPendingAuthorization()

        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertEqual(try fixture.store.snapshot(now: fixture.initialNow).pendingStatus, .none)
    }

    func testFinalizationExpiryDoesNotRecreateSupersededAuthorization() async throws {
        let persistence = ExpirationOAuthSecureState()
        let store = NativeOAuthAuthorizationStore(secureState: persistence)
        let initialNow = try XCTUnwrap(ISO8601DateFormatter().date(from: "2027-01-01T00:00:00Z"))
        try seedPending(store, now: initialNow)
        let claimed = try XCTUnwrap(try store.claimCallback(for: callbackURL(), now: initialNow))
        let clock = ExpirySupersessionClock(
            store: store,
            activeNow: initialNow,
            expiredNow: initialNow.addingTimeInterval(301)
        )
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            bootstrapSession: false
        )
        let coordinator = NativeOAuthAuthorizationCoordinator(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: IsolatedHTTPCookieStorage.make()),
            store: store,
            now: { clock.now() }
        )
        clock.discardOnRead(2)

        await coordinator.finalize(claimed)

        let snapshot = try store.snapshot(now: initialNow.addingTimeInterval(301))
        XCTAssertEqual(snapshot.pendingStatus, .none)
        XCTAssertNil(snapshot.result)
        XCTAssertNil(coordinator.result)
        XCTAssertNil(coordinator.errorMessage)
    }

    private func makeFixture(
        persistence: ExpirationOAuthSecureState = ExpirationOAuthSecureState(),
        prepareStore: (NativeOAuthAuthorizationStore, Date) throws -> Void = { _, _ in }
    ) throws -> ExpirationFixture {
        let now = try XCTUnwrap(ISO8601DateFormatter().date(from: "2027-01-01T00:00:00Z"))
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            bootstrapSession: false
        )
        let store = NativeOAuthAuthorizationStore(secureState: persistence)
        try prepareStore(store, now)
        let clock = ExpirationClock(now: now)
        return ExpirationFixture(
            coordinator: NativeOAuthAuthorizationCoordinator(
                client: client,
                sessionManager: SessionManager(client: client, cookieStorage: IsolatedHTTPCookieStorage.make()),
                store: store,
                now: { clock.now }
            ),
            persistence: persistence,
            store: store,
            initialNow: now,
            clock: clock
        )
    }

    private func seedPending(_ store: NativeOAuthAuthorizationStore, now: Date) throws {
        XCTAssertTrue(store.save(
            flowId: "callback-flow",
            provider: .github,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
    }

    private func callbackURL() throws -> URL {
        try XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=callback-flow&completion_token=token"
        ))
    }
}

private struct ExpirationFixture {
    let coordinator: NativeOAuthAuthorizationCoordinator
    let persistence: ExpirationOAuthSecureState
    let store: NativeOAuthAuthorizationStore
    let initialNow: Date
    let clock: ExpirationClock
}

private final class ExpirationClock: @unchecked Sendable {
    private let lock = NSLock()
    private var storedNow: Date

    init(now: Date) {
        storedNow = now
    }

    var now: Date {
        get { lock.withLock { storedNow } }
        set { lock.withLock { storedNow = newValue } }
    }
}

private final class ExpirySupersessionClock: @unchecked Sendable {
    private let lock = NSLock()
    private let store: NativeOAuthAuthorizationStore
    private let activeNow: Date
    private let expiredNow: Date
    private var readsUntilDiscard: Int?

    init(store: NativeOAuthAuthorizationStore, activeNow: Date, expiredNow: Date) {
        self.store = store
        self.activeNow = activeNow
        self.expiredNow = expiredNow
    }

    func discardOnRead(_ count: Int) {
        lock.withLock { readsUntilDiscard = count }
    }

    func now() -> Date {
        let shouldDiscard = lock.withLock {
            guard let remaining = readsUntilDiscard else { return false }
            readsUntilDiscard = remaining - 1
            return remaining == 1
        }
        guard shouldDiscard else { return activeNow }
        try? store.discardAuthorization()
        return expiredNow
    }
}

private final class ExpirationOAuthSecureState: NativeOAuthSecureStatePersisting, @unchecked Sendable {
    private let lock = NSLock()
    private var data: Data?
    var failNextWrite = false

    func read() -> Data? {
        lock.withLock { data }
    }

    func write(_ data: Data) -> Bool {
        lock.withLock {
            if failNextWrite {
                failNextWrite = false
                return false
            }
            self.data = data
            return true
        }
    }

    func delete() {
        lock.withLock { data = nil }
    }
}

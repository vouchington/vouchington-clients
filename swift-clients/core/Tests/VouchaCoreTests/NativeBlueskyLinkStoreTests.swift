@testable import VouchaCore
import XCTest

final class NativeBlueskyLinkStoreTests: XCTestCase {
    private let verifier = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

    func testCompletionProofMatchesRFC7636S256Vector() {
        let proof = NativeAuthorizationCompletionProof.fromVerifier(
            "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
        )

        XCTAssertEqual(proof.challenge, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")
    }

    func testGeneratedCompletionProofHasHighEntropyBase64URLShape() throws {
        let first = try NativeAuthorizationCompletionProof.generate()
        let second = try NativeAuthorizationCompletionProof.generate()

        XCTAssertNotEqual(first.verifier, second.verifier)
        XCTAssertNotEqual(first.challenge, second.challenge)
        XCTAssertNotNil(first.verifier.range(of: "^[A-Za-z0-9_-]{43}$", options: .regularExpression))
        XCTAssertNotNil(first.challenge.range(of: "^[A-Za-z0-9_-]{43}$", options: .regularExpression))
        XCTAssertEqual(first, NativeAuthorizationCompletionProof.fromVerifier(first.verifier))
    }

    func testPendingFlowSurvivesStoreRecreation() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        NativeBlueskyLinkStore(defaults: defaults).save(
            flowId: "flow",
            completionProofVerifier: verifier,
            now: Date(timeIntervalSince1970: 10)
        )
        XCTAssertEqual(
            try NativeBlueskyLinkStore(defaults: defaults).pending(now: Date(timeIntervalSince1970: 20))?.flowId,
            "flow"
        )
    }

    func testCallbackRequiresFixedRouteMatchingFlowAndUnexpiredState() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: verifier, now: Date(timeIntervalSince1970: 10))
        let callback = try store.callback(
            for:
            XCTUnwrap(URL(string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=secret")),
            now: Date(timeIntervalSince1970: 20)
        )
        XCTAssertEqual(
            callback,
            try .completion(XCTUnwrap(store.pending(now: Date(timeIntervalSince1970: 20))))
        )
        XCTAssertEqual(try store.pending(now: Date(timeIntervalSince1970: 20))?.flowId, "flow")
    }

    func testClaimCallbackPersistsFinalizationAcrossRecreationAndRejectsDuplicateReplay() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: verifier, now: Date(timeIntervalSince1970: 10))
        let url = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=secret"
        ))

        guard case let .completion(claimed)? = try store.claimCallback(
            for: url,
            now: Date(timeIntervalSince1970: 20)
        ) else { return XCTFail("Expected completion callback to be claimed") }
        XCTAssertEqual(claimed.completionToken, "secret")
        XCTAssertEqual(claimed.completionProofVerifier, verifier)
        XCTAssertNil(try store.claimCallback(for: url, now: Date(timeIntervalSince1970: 20)))
        let recreated = NativeBlueskyLinkStore(defaults: defaults)
        XCTAssertEqual(try recreated.pending(now: Date(timeIntervalSince1970: 20)), claimed)
        XCTAssertTrue(try XCTUnwrap(recreated.pending(now: Date(timeIntervalSince1970: 20))).isFinalizing)
        XCTAssertTrue(store.isCallbackURL(url))
    }

    func testProviderFailureClearsAwaitingFlowButCannotEraseFinalization() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: verifier)
        let completion = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=secret"
        ))
        let failure = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&bluesky_error=access_denied"
        ))

        XCTAssertNotNil(try store.claimCallback(for: completion))
        XCTAssertNil(try store.claimCallback(for: failure))
        XCTAssertEqual(try store.pending()?.completionToken, "secret")

        try store.clear()
        store.save(flowId: "flow", completionProofVerifier: verifier)
        XCTAssertEqual(try store.claimCallback(for: failure), .failure(flowId: "flow", code: "access_denied"))
        XCTAssertNil(try store.pending())
    }

    func testTransientFinalizationFailureRemainsRetryableAcrossColdStart() async throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(
            flowId: "flow",
            completionProofVerifier: verifier,
            now: Date(timeIntervalSince1970: 10)
        )
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=secret"
        ))
        guard case let .completion(pending)? = try store.claimCallback(
            for: callback,
            now: Date(timeIntervalSince1970: 20)
        ) else { return XCTFail("Expected completion callback to be claimed") }

        let firstAttempt = await store.finalize(
            pending,
            now: Date(timeIntervalSince1970: 20),
            complete: { _, _, _ in throw FinalizationTestError.transient },
            confirmAttached: { false }
        )

        XCTAssertFalse(firstAttempt)
        XCTAssertEqual(store.takeResult(), .finalizationFailure)
        XCTAssertEqual(try store.pending(now: Date(timeIntervalSince1970: 20)), pending)

        let recreated = NativeBlueskyLinkStore(defaults: defaults)
        let retried = try await recreated.finalize(
            XCTUnwrap(try recreated.pending(now: Date(timeIntervalSince1970: 20))),
            now: Date(timeIntervalSince1970: 20),
            complete: { _, _, _ in },
            confirmAttached: { false }
        )

        XCTAssertTrue(retried)
        XCTAssertEqual(recreated.takeResult(), .success)
        XCTAssertNil(try recreated.pending(now: Date(timeIntervalSince1970: 20)))
    }

    func testLostCompletionResponseClearsOnlyAfterIdentityConfirmsAttachment() async throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: verifier)
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=secret"
        ))
        guard case let .completion(pending)? = try store.claimCallback(for: callback) else {
            return XCTFail("Expected completion callback to be claimed")
        }

        let completed = await store.finalize(
            pending,
            complete: { _, _, _ in throw FinalizationTestError.lostResponse },
            confirmAttached: { true }
        )

        XCTAssertTrue(completed)
        XCTAssertEqual(store.takeResult(), .success)
        XCTAssertNil(try store.pending())
    }

    func testSecureDeletionFailureCannotPublishFinalizationSuccess() async throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let persistence = DeletionFailingBlueskySecureState()
        let store = NativeBlueskyLinkStore(pendingState: persistence, resultDefaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: verifier)
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=secret"
        ))
        guard case let .completion(pending)? = try store.claimCallback(for: callback) else {
            return XCTFail("Expected completion callback to be claimed")
        }
        persistence.failNextDelete = true

        let completed = await store.finalize(
            pending,
            complete: { _, _, _ in },
            confirmAttached: { false }
        )

        XCTAssertFalse(completed)
        XCTAssertEqual(store.takeResult(), .finalizationFailure)
        XCTAssertEqual(try store.pending(), pending)

        let retried = await store.finalize(
            pending,
            complete: { _, _, _ in },
            confirmAttached: { false }
        )
        XCTAssertTrue(retried)
        XCTAssertEqual(store.takeResult(), .success)
        XCTAssertNil(try store.pending())
    }

    func testOverlappingFinalizerCannotRaceSuccessWithLaggingTransportFailure() async throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let callbackStore = NativeBlueskyLinkStore(defaults: defaults)
        callbackStore.save(flowId: "flow", completionProofVerifier: verifier)
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/bluesky/callback?flow_id=flow&completion_token=secret"
        ))
        guard case let .completion(pending)? = try callbackStore.claimCallback(for: callback) else {
            return XCTFail("Expected completion callback to be claimed")
        }
        let transportGate = FinalizationTransportGate()
        let winningFinalizer = Task {
            await callbackStore.finalize(
                pending,
                complete: { _, _, _ in await transportGate.run() },
                confirmAttached: { false }
            )
        }
        await transportGate.waitUntilStarted()

        let coldResumeStore = NativeBlueskyLinkStore(defaults: defaults)
        let overlappingResult = try await coldResumeStore.finalize(
            XCTUnwrap(try coldResumeStore.pending()),
            complete: { _, _, _ in
                XCTFail("A leased flow must not issue an overlapping completion request")
                throw FinalizationTestError.transient
            },
            confirmAttached: {
                XCTFail("A rejected overlapping finalizer must not reconcile identity")
                return false
            }
        )

        XCTAssertFalse(overlappingResult)
        await transportGate.release()
        let winningResult = await winningFinalizer.value
        XCTAssertTrue(winningResult)
        XCTAssertEqual(callbackStore.takeResult(), .success)
        XCTAssertNil(try callbackStore.pending())
    }

    func testPendingStatusReportsExpiryOnceAndRecoversToNone() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: verifier, now: Date(timeIntervalSince1970: 10))

        XCTAssertEqual(try store.pendingStatus(now: Date(timeIntervalSince1970: 700)), .expired)
        XCTAssertEqual(try store.pendingStatus(now: Date(timeIntervalSince1970: 700)), .none)
    }

    func testPendingFlowExpiresAtExactDeadline() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "flow", completionProofVerifier: verifier, now: Date(timeIntervalSince1970: 10))

        XCTAssertEqual(
            try store.pending(now: Date(timeIntervalSince1970: 609.999))?.flowId,
            "flow"
        )
        XCTAssertEqual(try store.pendingStatus(now: Date(timeIntervalSince1970: 610)), .expired)
    }

    func testMismatchedAndExpiredCallbacksAreRejected() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.save(flowId: "expected", completionProofVerifier: verifier, now: Date(timeIntervalSince1970: 10))
        XCTAssertNil(try store.callback(
            for:
            XCTUnwrap(URL(string: "voucha://auth/bluesky/callback?flow_id=other&completion_token=secret")),
            now: Date(timeIntervalSince1970: 20)
        ))
        XCTAssertNil(try store.pending(now: Date(timeIntervalSince1970: 700)))
    }

    func testTerminalResultSurvivesColdStartAndIsConsumedOnce() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        NativeBlueskyLinkStore(defaults: defaults).recordResult(.finalizationFailure)
        let recreated = NativeBlueskyLinkStore(defaults: defaults)
        XCTAssertEqual(recreated.takeResult(), .finalizationFailure)
        XCTAssertNil(recreated.takeResult())
    }

    func testFinalizingProgressCanBePublishedBeforeTerminalResult() throws {
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let store = NativeBlueskyLinkStore(defaults: defaults)
        store.recordResult(.finalizing)
        XCTAssertEqual(store.takeResult(), .finalizing)
    }
}

private enum FinalizationTestError: Error {
    case transient
    case lostResponse
}

private final class DeletionFailingBlueskySecureState:
    NativePendingStatePersisting,
    @unchecked Sendable {
    private let lock = NSLock()
    private var data: Data?
    var failNextDelete = false

    func read() -> Data? {
        lock.withLock { data }
    }

    func write(_ data: Data) -> Bool {
        lock.withLock { self.data = data }
        return true
    }

    func delete() throws {
        try lock.withLock {
            if failNextDelete {
                failNextDelete = false
                throw NativeOAuthSecureStateError.unavailable
            }
            data = nil
        }
    }
}

private actor FinalizationTransportGate {
    private var started = false
    private var released = false
    private var startWaiters: [CheckedContinuation<Void, Never>] = []
    private var releaseWaiters: [CheckedContinuation<Void, Never>] = []

    func run() async {
        started = true
        startWaiters.forEach { $0.resume() }
        startWaiters.removeAll()
        guard !released else { return }
        await withCheckedContinuation { releaseWaiters.append($0) }
    }

    func waitUntilStarted() async {
        guard !started else { return }
        await withCheckedContinuation { startWaiters.append($0) }
    }

    func release() {
        released = true
        releaseWaiters.forEach { $0.resume() }
        releaseWaiters.removeAll()
    }
}

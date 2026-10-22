import Foundation
import VouchaModels
import XCTest

final class MembershipPurchaseSessionTests: XCTestCase {
    func testConflictDoesNotOpenASheetOrPortal() async throws {
        let conflict = try makeVouchaDecoder().decode(
            NativeMembershipPurchaseConflict.self,
            from: nativeMembershipFixture("native.memberships.purchase-intent.conflict.default")
        )
        let harness = try Harness(intent: .conflict(conflict))
        let outcome = try await harness.purchase(overview: harness.empty)
        XCTAssertFalse(outcome.sheetRequested)
        XCTAssertEqual(outcome.refusal, .conflict)
        XCTAssertEqual(outcome.eligibleAt, "2027-01-01T00:00:00.000Z")
        XCTAssertEqual(outcome.managementDestination, "billing_portal")
        XCTAssertTrue(harness.store.launches.isEmpty)
        XCTAssertFalse(outcome.managementDestination?.contains("http") ?? false)
    }

    func testAdmittedApplePurchasePostsFixtureEvidenceAndRemovesTheOutbox() async throws {
        let harness = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        harness.transport.submissions = try [.created(verification("native.memberships.verification.pending.default"))]
        harness.transport.statuses = try [verification("native.memberships.verification-status.verified.default")]
        let outcome = try await harness.purchase(overview: harness.empty)
        XCTAssertTrue(outcome.sheetRequested)
        XCTAssertEqual(outcome.verificationStatus, "verified")
        XCTAssertFalse(outcome.replayed)
        XCTAssertEqual(harness.store.launches.count, 1)
        XCTAssertEqual(harness.keys.issued, ["idem-1"])
        let body = try XCTUnwrap(harness.transport.verificationBodies.first)
        XCTAssertEqual(body.idempotencyKey, "idem-1")
        XCTAssertEqual(body.purchaseIntentId, "00000000-0000-7000-8000-000000000803")
        XCTAssertEqual(body.provider, "apple_app_store")
        let stored = await harness.outbox.contains(key: "idem-1")
        XCTAssertFalse(stored)
        let launch = try JSONEncoder().encode(harness.store.launches[0])
        XCTAssertTrue(String(decoding: launch, as: UTF8.self).contains("00000000-0000-7000-8000-000000000803"))
    }

    func testStripeGoogleAndMicrosoftFixturesDoNotOpenBilling() async throws {
        let cases: [(String, NativeMembershipStore)] = [
            ("native.memberships.purchase-intent.stripe.default", .appleIOS),
            ("native.memberships.purchase-intent.google.default", .googlePlay),
            ("native.memberships.purchase-intent.microsoft.default", .microsoftStore)
        ]
        for (name, surface) in cases {
            let harness = try Harness(intent: .created(purchase(name)))
            let outcome = try await harness.purchase(overview: harness.empty, surface: surface)
            XCTAssertFalse(outcome.sheetRequested, name)
            XCTAssertNotNil(outcome.refusal, name)
            XCTAssertTrue(harness.store.launches.isEmpty, name)
        }
    }

    func testReplacementAndReplayAndRetryReuseTheAttemptKey() async throws {
        let lifecycle = try decodeOverview("native.memberships.me.lifecycle.default")
        let blocked = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        let blockedOutcome = try await blocked.purchase(overview: lifecycle)
        XCTAssertEqual(blockedOutcome.refusal, .replacementUnavailable)
        XCTAssertTrue(blocked.store.launches.isEmpty)

        let replay = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        replay.transport
            .submissions = try [.replayed(verification("native.memberships.verification-status.verified.default"))]
        let replayed = try await replay.purchase(overview: replay.empty)
        XCTAssertTrue(replayed.replayed)
        XCTAssertEqual(replayed.verificationStatus, "verified")
        let replayStored = await replay.outbox.contains(key: "idem-1")
        XCTAssertFalse(replayStored)

        let retried = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        retried.transport.submitError = URLError(.networkConnectionLost)
        retried.transport
            .submissions = try [.created(verification("native.memberships.verification-status.verified.default"))]
        let retriedOutcome = try await retried.purchase(overview: retried.empty)
        XCTAssertEqual(retried.transport.verificationBodies.map(\.idempotencyKey), ["idem-1", "idem-1"])
        XCTAssertEqual(retriedOutcome.verificationStatus, "verified")
        let retriedStored = await retried.outbox.contains(key: "idem-1")
        XCTAssertFalse(retriedStored)
    }

    func testPollingWaitsForProcessingTimeBackoffAndForegroundRefresh() async throws {
        let pending = try verification("native.memberships.verification-status.pending.default")
        let verified = try verification("native.memberships.verification-status.verified.default")
        let harness = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        harness.transport.submissions = [.created(pending)]
        harness.transport.statuses = [pending, pending, pending, pending, pending, verified]
        let outcome = try await harness.purchase(overview: harness.empty)
        XCTAssertEqual(harness.clock.sleeps, [1, 2, 4, 8, 15])
        XCTAssertEqual(outcome.verificationStatus, "verified")

        let scheduled = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        scheduled.transport.submissions = [.created(pending)]
        scheduled.transport.statuses = try [pendingVerification(reason: "purchase_pending"), verified]
        scheduled.transport.overviews = try [processingOverview(at: "2026-10-21T18:00:00.000Z")]
        scheduled.clock.now = try XCTUnwrap(ISO8601DateFormatter().date(from: "2026-10-21T17:00:00Z"))
        _ = try await scheduled.purchase(overview: scheduled.empty)
        XCTAssertEqual(scheduled.clock.sleeps, [3_600])

        let foreground = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        foreground.transport.submissions = [.created(pending)]
        foreground.transport.statuses = [pending, verified]
        foreground.transport.onVerification = { foreground.signals.noteForeground() }
        _ = try await foreground.purchase(overview: foreground.empty)
        XCTAssertTrue(foreground.clock.sleeps.isEmpty)
    }

    func testReplayRefreshesMembershipAndPendingPollingSurvivesAFailedHint() async throws {
        let replay = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        replay.transport
            .submissions = try [.replayed(verification("native.memberships.verification-status.verified.default"))]
        let replayed = try await replay.purchase(overview: replay.empty)
        XCTAssertTrue(replayed.replayed)
        XCTAssertEqual(replay.transport.membershipCalls, 1)

        let pending = try verification("native.memberships.verification-status.pending.default")
        let verified = try verification("native.memberships.verification-status.verified.default")
        let interrupted = try Harness(intent: .created(purchase("native.memberships.purchase-intent.apple.default")))
        interrupted.transport.submissions = [.created(pending)]
        interrupted.transport.statuses = [pending, verified]
        interrupted.transport.membershipError = URLError(.networkConnectionLost)
        let outcome = try await interrupted.purchase(overview: interrupted.empty)
        XCTAssertEqual(outcome.verificationStatus, "verified")
        XCTAssertEqual(interrupted.clock.sleeps, [1])
    }
}

private final class Harness {
    let transport: ScriptedTransport
    let store = RecordingStore()
    let outbox = MemoryMembershipEvidenceOutbox()
    let keys = QueueKeys(values: ["idem-1", "idem-2"])
    let clock = ManualClock(now: Date(timeIntervalSince1970: 0))
    let signals = MembershipPurchaseSignals()
    let empty: NativeMembershipOverview
    let products: [MembershipCatalogProduct]

    init(intent: MembershipIntentSubmission) throws {
        transport = ScriptedTransport(intent: intent)
        empty = try decodeOverview("native.memberships.me.default")
        products = try makeVouchaDecoder().decode(
            MembershipPlansResponse.self,
            from: nativeMembershipFixture("native.memberships.plans.default")
        ).products
    }

    func purchase(
        overview: NativeMembershipOverview,
        surface: NativeMembershipStore = .appleIOS
    ) async throws -> MembershipPurchaseOutcome {
        let session = MembershipPurchaseSession(
            transport: transport,
            store: store,
            outbox: outbox,
            keys: keys,
            clock: clock,
            signals: signals
        )
        return try await session.purchase(
            productId: transport.intentProductId,
            provider: transport.intentProvider,
            surface: surface,
            products: products,
            overview: overview
        )
    }
}

private final class ScriptedTransport: MembershipPurchaseTransport, @unchecked Sendable {
    var intent: MembershipIntentSubmission
    var submissions: [MembershipVerificationSubmissionResult] = []
    var statuses: [NativeMembershipVerificationResponse] = []
    var overviews: [NativeMembershipOverview] = []
    var submitError: Error?
    var membershipError: Error?
    var onVerification: () -> Void = {}
    private(set) var membershipCalls = 0
    private(set) var verificationBodies: [NativeMembershipVerificationSubmission] = []
    private var submitCount = 0

    init(intent: MembershipIntentSubmission) {
        self.intent = intent
    }

    var intentProductId: String {
        if case let .created(response) = intent {
            return response.purchaseIntent.productId
        }
        return "00000000-0000-7000-8000-000000000701"
    }

    var intentProvider: String {
        if case let .created(response) = intent {
            return response.purchaseIntent.provider
        }
        return "apple_app_store"
    }

    func createIntent(_ body: NativeMembershipIntentBody) async throws -> MembershipIntentSubmission {
        _ = body
        return intent
    }

    func submitVerification(
        _ body: NativeMembershipVerificationSubmission
    ) async throws -> MembershipVerificationSubmissionResult {
        verificationBodies.append(body)
        submitCount += 1
        if submitCount == 1, let submitError {
            throw submitError
        }
        return submissions.removeFirst()
    }

    func verification(id _: String) async throws -> NativeMembershipVerificationResponse {
        onVerification()
        onVerification = {}
        return statuses.removeFirst()
    }

    func membership() async throws -> NativeMembershipOverview {
        membershipCalls += 1
        if let membershipError {
            throw membershipError
        }
        return overviews.isEmpty ? try decodeOverview("native.memberships.me.default") : overviews[0]
    }
}

private final class RecordingStore: MembershipStoreAdapter, @unchecked Sendable {
    private(set) var launches: [DecodedJSONValue] = []

    func purchase(launch: DecodedJSONValue) async throws -> MembershipStorePurchase {
        launches.append(launch)
        let evidence = try JSONDecoder().decode(
            DecodedJSONValue.self,
            from: Data(#"{"signed_transaction":"fixture-signed-transaction"}"#.utf8)
        )
        return MembershipStorePurchase(transactionIdentity: "transaction-\(launches.count)", evidence: evidence)
    }
}

private final class QueueKeys: MembershipIdempotencyKeys, @unchecked Sendable {
    var values: [String]
    private(set) var issued: [String] = []

    init(values: [String]) {
        self.values = values
    }

    func next() -> String {
        let key = values.removeFirst()
        issued.append(key)
        return key
    }
}

private final class ManualClock: MembershipPurchaseClock, @unchecked Sendable {
    var now: Date
    private(set) var sleeps: [TimeInterval] = []

    init(now: Date) {
        self.now = now
    }

    func sleep(seconds: TimeInterval) async throws {
        sleeps.append(seconds)
        now = now.addingTimeInterval(seconds)
    }
}

private func verification(_ name: String) throws -> NativeMembershipVerificationResponse {
    try makeVouchaDecoder().decode(
        NativeMembershipVerificationResponse.self,
        from: nativeMembershipFixture(name)
    )
}

private func pendingVerification(reason: String) throws -> NativeMembershipVerificationResponse {
    let body = """
    {"verification":{"created_at":"2026-09-09T12:00:00.000Z","id":"00000000-0000-7000-8000-000000000807","provider":"apple_app_store","reason_code":"\(
        reason
    )","status":"pending"}}
    """
    return try makeVouchaDecoder().decode(NativeMembershipVerificationResponse.self, from: Data(body.utf8))
}

private func processingOverview(at instant: String) throws -> NativeMembershipOverview {
    let body = """
    {"management":null,"membership":null,"pending":{"financial_operations":[],"grants":0,"switches":[],"verifications":[{"id":"00000000-0000-7000-8000-000000000807","next_processing_at":"\(
        instant
    )","provider":"apple_app_store","reason_code":"purchase_pending","status":"pending"}]},"sources":[]}
    """
    return try makeVouchaDecoder().decode(NativeMembershipOverview.self, from: Data(body.utf8))
}

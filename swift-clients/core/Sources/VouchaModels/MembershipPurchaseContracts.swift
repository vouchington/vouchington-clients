import Foundation

public enum MembershipPurchaseError: Error, Equatable {
    case verificationDidNotFinish
}

public enum MembershipIntentSubmission: Sendable {
    case created(NativeMembershipPurchaseIntentResponse)
    case conflict(NativeMembershipPurchaseConflict)
}

public enum MembershipVerificationSubmissionResult: Sendable {
    case created(NativeMembershipVerificationResponse)
    case replayed(NativeMembershipVerificationResponse)

    public var verification: NativeMembershipVerification {
        switch self {
        case let .created(response), let .replayed(response):
            response.verification
        }
    }

    public var replayed: Bool {
        if case .replayed = self {
            return true
        }
        return false
    }
}

public struct MembershipStorePurchase: Sendable {
    public let transactionIdentity: String
    public let evidence: DecodedJSONValue

    public init(transactionIdentity: String, evidence: DecodedJSONValue) {
        self.transactionIdentity = transactionIdentity
        self.evidence = evidence
    }
}

public struct MembershipPurchaseOutcome: Equatable, Sendable {
    public let sheetRequested: Bool
    public let idempotencyKey: String
    public let refusal: MembershipPurchaseRefusal?
    public let eligibleAt: String?
    public let managementDestination: String?
    public let verificationStatus: String?
    public let verificationReason: String?
    public let replayed: Bool
}

public protocol MembershipPurchaseTransport: Sendable {
    func createIntent(_ body: NativeMembershipIntentBody) async throws -> MembershipIntentSubmission
    func submitVerification(
        _ body: NativeMembershipVerificationSubmission
    ) async throws -> MembershipVerificationSubmissionResult
    func verification(id: String) async throws -> NativeMembershipVerificationResponse
    func membership() async throws -> NativeMembershipOverview
}

public protocol MembershipStoreAdapter: Sendable {
    func purchase(launch: DecodedJSONValue) async throws -> MembershipStorePurchase
}

public protocol MembershipEvidenceOutbox: Sendable {
    func save(key: String, evidence: DecodedJSONValue) async
    func remove(key: String) async
    func contains(key: String) async -> Bool
}

public protocol MembershipIdempotencyKeys: Sendable {
    func next() -> String
}

public protocol MembershipPurchaseClock: Sendable {
    var now: Date { get }
    func sleep(seconds: TimeInterval) async throws
}

public final class MembershipPurchaseSignals: @unchecked Sendable {
    private let lock = NSLock()
    private var refresh = false

    public init() {}

    public func noteForeground() {
        lock.lock()
        refresh = true
        lock.unlock()
    }

    public func noteStoreUpdate() {
        noteForeground()
    }

    func consumeRefresh() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        let value = refresh
        refresh = false
        return value
    }
}

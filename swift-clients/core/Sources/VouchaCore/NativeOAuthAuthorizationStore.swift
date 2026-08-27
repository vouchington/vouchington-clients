import Foundation

public final class NativeOAuthAuthorizationStore: @unchecked Sendable {
    struct StoredState: Codable, Equatable {
        let pending: PendingNativeOAuthAuthorization?
        let result: NativeOAuthAuthorizationResult?
    }

    static let lock = NSLock()
    private static var leasedFlowIds: Set<String> = []
    let secureState: any NativeOAuthSecureStatePersisting

    #if canImport(Security)
        public convenience init() {
            self.init(secureState: KeychainNativePendingState(
                service: "ai.voucha.native-oauth-authorization",
                account: "pending-v1"
            ))
        }
    #endif

    convenience init(defaults: UserDefaults) {
        self.init(secureState: UserDefaultsNativePendingState(
            key: "nativeOAuthPendingAuthorization",
            defaults: defaults
        ))
    }

    public init(secureState: any NativeOAuthSecureStatePersisting) {
        self.secureState = secureState
    }

    @discardableResult
    public func save(
        flowId: String,
        provider: NativeOAuthProvider,
        purpose: NativeOAuthAuthorizationPurpose,
        completionProofVerifier: String,
        expiresAt: Date,
        now: Date = Date()
    ) -> Bool {
        Self.lock.withLock {
            let state: StoredState?
            do {
                state = try readState()
            } catch {
                return false
            }
            guard state?.result == nil else { return false }
            let status = pendingStatus(for: state?.pending, now: now)
            if case .active = status {
                return false
            }
            return persist(StoredState(
                pending: PendingNativeOAuthAuthorization(
                    flowId: flowId,
                    provider: provider,
                    purpose: purpose,
                    completionProofVerifier: completionProofVerifier,
                    expiresAt: expiresAt,
                    completionToken: nil
                ),
                result: nil
            ))
        }
    }

    public func pendingStatus(now: Date = Date()) -> PendingNativeOAuthAuthorizationStatus {
        Self.lock.withLock { (try? pendingStatusUnlocked(now: now)) ?? .none }
    }

    public func pending(now: Date = Date()) -> PendingNativeOAuthAuthorization? {
        if case let .active(pending) = pendingStatus(now: now) {
            return pending
        }
        return nil
    }

    public func result() -> NativeOAuthAuthorizationResult? {
        Self.lock.withLock { (try? readState())?.result }
    }

    public func snapshot(now: Date = Date()) throws -> NativeOAuthAuthorizationSnapshot {
        try Self.lock.withLock {
            let state = try readState()
            return NativeOAuthAuthorizationSnapshot(
                pendingStatus: pendingStatus(for: state?.pending, now: now),
                result: state?.result
            )
        }
    }

    public func acknowledgeResult() throws {
        let didDelete = try Self.lock.withLock {
            guard try readState()?.result != nil else { return false }
            try secureState.delete()
            return true
        }
        if didDelete {
            publishChange()
        }
    }

    public func clearPending() throws {
        let didClear = try Self.lock.withLock {
            guard let current = try readState(), current.pending != nil else { return false }
            if let result = current.result {
                guard persist(StoredState(pending: nil, result: result)) else {
                    throw NativeOAuthSecureStateError.unavailable
                }
            } else {
                try secureState.delete()
            }
            return true
        }
        if didClear {
            publishChange()
        }
    }

    public func acquireFinalizationLease(
        for pending: PendingNativeOAuthAuthorization,
        now: Date = Date()
    ) throws -> Bool {
        try Self.lock.withLock {
            guard case let .active(current) = try pendingStatusUnlocked(now: now),
                  current == pending,
                  !Self.leasedFlowIds.contains(pending.flowId)
            else { return false }
            Self.leasedFlowIds.insert(pending.flowId)
            return true
        }
    }

    public func releaseFinalizationLease(flowId: String) {
        _ = Self.lock.withLock { Self.leasedFlowIds.remove(flowId) }
    }

    public func complete(
        _ pending: PendingNativeOAuthAuthorization,
        with result: NativeOAuthAuthorizationResult
    ) -> Bool {
        let didPersist = Self.lock.withLock {
            guard (try? readState())?.pending == pending else { return false }
            return persist(StoredState(pending: nil, result: result))
        }
        if didPersist {
            publishChange()
        }
        return didPersist
    }

    public func record(_ result: NativeOAuthAuthorizationResult) throws {
        try Self.lock.withLock {
            guard persist(StoredState(pending: nil, result: result)) else {
                throw NativeOAuthSecureStateError.unavailable
            }
        }
        publishChange()
    }
}

private extension NativeOAuthAuthorizationStore {
    private func pendingStatusUnlocked(now: Date) throws -> PendingNativeOAuthAuthorizationStatus {
        try pendingStatus(for: readState()?.pending, now: now)
    }

    private func pendingStatus(
        for pending: PendingNativeOAuthAuthorization?,
        now: Date
    ) -> PendingNativeOAuthAuthorizationStatus {
        guard let pending else { return .none }
        guard now < pending.expiresAt else {
            return .expired(pending)
        }
        return .active(pending)
    }

    private func readState() throws -> StoredState? {
        guard let data = try secureState.read() else { return nil }
        guard let decoded = try? JSONDecoder().decode(StoredState.self, from: data) else {
            try secureState.delete()
            return nil
        }
        return decoded
    }

    private func persist(_ value: StoredState) -> Bool {
        guard let data = try? JSONEncoder().encode(value) else { return false }
        return secureState.write(data)
    }

    private func publishChange() {
        NotificationCenter.default.post(name: .nativeOAuthAuthorizationDidChange, object: nil)
    }
}

import Foundation

public final class NativeBlueskyLinkStore: @unchecked Sendable {
    private static let resultKey = "nativeBlueskyLinkResult"
    public static let lifetime: TimeInterval = 10 * 60
    private static let lock = NSLock()
    private static var leasedFinalizationFlowIds: Set<String> = []
    private let pendingState: any NativePendingStatePersisting
    private let resultDefaults: UserDefaults

    public convenience init() {
        self.init(
            pendingState: KeychainNativePendingState(
                service: "ai.voucha.native-bluesky-link",
                account: "pending-v1"
            ),
            resultDefaults: .standard
        )
    }

    public convenience init(defaults: UserDefaults) {
        self.init(
            pendingState: UserDefaultsNativePendingState(key: "nativeBlueskyPendingLink", defaults: defaults),
            resultDefaults: defaults
        )
    }

    init(pendingState: any NativePendingStatePersisting, resultDefaults: UserDefaults) {
        self.pendingState = pendingState
        self.resultDefaults = resultDefaults
    }

    @discardableResult
    public func save(flowId: String, completionProofVerifier: String, now: Date = Date()) -> Bool {
        let pending = PendingNativeBlueskyLink(
            flowId: flowId,
            completionProofVerifier: completionProofVerifier,
            startedAt: now,
            completionToken: nil
        )
        return Self.lock.withLock { persist(pending) }
    }

    public func pending(now: Date = Date()) throws -> PendingNativeBlueskyLink? {
        switch try pendingStatus(now: now) {
        case let .active(pending): pending
        case .none, .expired: nil
        }
    }

    public func pendingStatus(now: Date = Date()) throws -> PendingNativeBlueskyLinkStatus {
        try Self.lock.withLock { try pendingStatusUnlocked(now: now) }
    }

    public func clear() throws {
        try Self.lock.withLock { try pendingState.delete() }
    }

    @discardableResult
    public func finalize(
        _ pending: PendingNativeBlueskyLink,
        now: Date = Date(),
        complete: (String, String, String) async throws -> Void,
        confirmAttached: () async -> Bool
    ) async -> Bool {
        guard let completionToken = pending.completionToken else { return false }
        let acquiredLease: Bool
        do {
            acquiredLease = try acquireFinalizationLease(for: pending, now: now)
        } catch {
            return recordFinalizationFailure()
        }
        guard acquiredLease else { return false }
        defer { releaseFinalizationLease(for: pending.flowId) }

        recordResult(.finalizing)
        do {
            try await complete(pending.flowId, completionToken, pending.completionProofVerifier)
        } catch {
            guard await confirmAttached() else { return recordFinalizationFailure() }
        }
        guard clearForFinalization(ifMatching: pending) else { return recordFinalizationFailure() }
        recordResult(.success)
        return true
    }

    public func recordResult(_ result: NativeBlueskyLinkResult) {
        Self.lock.withLock { resultDefaults.set(result.rawValue, forKey: Self.resultKey) }
        NotificationCenter.default.post(name: .blueskyLinkResultDidChange, object: resultDefaults)
    }

    public func takeResult() -> NativeBlueskyLinkResult? {
        Self.lock.withLock {
            defer { resultDefaults.removeObject(forKey: Self.resultKey) }
            return resultDefaults.string(forKey: Self.resultKey).flatMap(NativeBlueskyLinkResult.init(rawValue:))
        }
    }

    public func callback(for url: URL, now: Date = Date()) throws -> NativeBlueskyCallback? {
        try Self.lock.withLock { try callbackUnlocked(for: url, now: now) }
    }

    public func claimCallback(for url: URL, now: Date = Date()) throws -> NativeBlueskyCallback? {
        try Self.lock.withLock {
            guard let callback = try callbackUnlocked(for: url, now: now) else { return nil }
            switch callback {
            case let .completion(pending):
                guard !pending.isFinalizing,
                      let token = completionToken(from: url)
                else { return nil }
                let finalizing = PendingNativeBlueskyLink(
                    flowId: pending.flowId,
                    completionProofVerifier: pending.completionProofVerifier,
                    startedAt: pending.startedAt,
                    completionToken: token
                )
                return persist(finalizing) ? .completion(finalizing) : nil
            case .failure:
                guard case let .active(pending) = try pendingStatusUnlocked(now: now),
                      !pending.isFinalizing
                else { return nil }
                try pendingState.delete()
                return callback
            }
        }
    }

    private func callbackUnlocked(for url: URL, now: Date) throws -> NativeBlueskyCallback? {
        guard isCallbackURL(url),
              case let .active(pending) = try pendingStatusUnlocked(now: now),
              let components = URLComponents(url: url, resolvingAgainstBaseURL: false),
              components.queryItems?.first(where: { $0.name == "flow_id" })?.value == pending.flowId
        else { return nil }
        let items = components.queryItems ?? []
        return if let token = items.first(where: { $0.name == "completion_token" })?.value, !token.isEmpty {
            .completion(pending)
        } else if let code = items.first(where: { $0.name == "bluesky_error" })?.value, !code.isEmpty {
            .failure(flowId: pending.flowId, code: code)
        } else {
            nil
        }
    }
}

private extension NativeBlueskyLinkStore {
    private func pendingStatusUnlocked(now: Date) throws -> PendingNativeBlueskyLinkStatus {
        guard let data = try pendingState.read() else { return .none }
        guard let pending = try? JSONDecoder().decode(PendingNativeBlueskyLink.self, from: data) else {
            try pendingState.delete()
            return .none
        }
        guard now < pending.expiresAt else {
            try pendingState.delete()
            return .expired
        }
        return .active(pending)
    }

    private func persist(_ pending: PendingNativeBlueskyLink) -> Bool {
        guard let data = try? JSONEncoder().encode(pending) else { return false }
        return pendingState.write(data)
    }

    private func acquireFinalizationLease(for pending: PendingNativeBlueskyLink, now: Date) throws -> Bool {
        try Self.lock.withLock {
            guard case let .active(current) = try pendingStatusUnlocked(now: now),
                  current == pending,
                  !Self.leasedFinalizationFlowIds.contains(pending.flowId)
            else { return false }
            Self.leasedFinalizationFlowIds.insert(pending.flowId)
            return true
        }
    }

    private func releaseFinalizationLease(for flowId: String) {
        _ = Self.lock.withLock { Self.leasedFinalizationFlowIds.remove(flowId) }
    }

    private func clearForFinalization(ifMatching pending: PendingNativeBlueskyLink) -> Bool {
        do {
            return try clear(ifMatching: pending)
        } catch {
            return false
        }
    }

    private func clear(ifMatching pending: PendingNativeBlueskyLink) throws -> Bool {
        try Self.lock.withLock {
            guard let data = try pendingState.read(),
                  let current = try? JSONDecoder().decode(PendingNativeBlueskyLink.self, from: data),
                  current == pending
            else { return false }
            try pendingState.delete()
            return true
        }
    }
}

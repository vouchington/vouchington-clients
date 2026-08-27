import Observation
import VouchaAPI
import VouchaModels
import VouchaPersistence

public enum FeatureFlagOverrideError: Error, Equatable {
    case remoteFlagsNotLoaded
    case unknownFlag(String)
}

public struct FeatureFlagPublicFetchToken: Sendable {
    fileprivate let authoritativeGeneration: UInt64
    fileprivate let fetchSequence: UInt64
}

@MainActor
@Observable
public final class FeatureFlagState {
    public private(set) var effective: [String: Bool] = [:]
    public private(set) var remote: [String: Bool] = [:]
    public private(set) var localOverrides: [String: Bool]
    public private(set) var hasLoadedRemote = false
    private let store: any FeatureFlagOverridePersisting
    private var authoritativeGeneration: UInt64 = 0
    private var nextPublicFetchSequence: UInt64 = 0
    private var latestAppliedPublicFetchSequence: UInt64 = 0

    public init(store: any FeatureFlagOverridePersisting) {
        self.store = store
        localOverrides = store.load()
    }

    public static func canManageDeviceOverrides(userRoles: [String]) -> Bool {
        userRoles.contains("administrator") || userRoles.contains("developer")
    }

    public func beginPublicFetch() -> FeatureFlagPublicFetchToken {
        nextPublicFetchSequence &+= 1
        return FeatureFlagPublicFetchToken(
            authoritativeGeneration: authoritativeGeneration,
            fetchSequence: nextPublicFetchSequence
        )
    }

    @discardableResult
    public func applyPublicResponse(
        _ response: FeatureFlagsResponse,
        for token: FeatureFlagPublicFetchToken
    ) -> Bool {
        guard token.authoritativeGeneration == authoritativeGeneration,
              token.fetchSequence > latestAppliedPublicFetchSequence
        else {
            return false
        }
        latestAppliedPublicFetchSequence = token.fetchSequence
        setRemote(response.flags)
        return true
    }

    public func applyRemote(_ flags: [String: Bool]) {
        advanceAuthoritativeGeneration()
        setRemote(flags)
    }

    public func applyGlobalNamespace(_ namespace: DynamicConfigNamespace) {
        guard namespace.namespace == "feature-flags" else { return }
        let flags = namespace.config.reduce(into: [String: Bool]()) { result, pair in
            guard case let .boolean(value) = pair.value else { return }
            result[pair.key] = value
        }
        advanceAuthoritativeGeneration()
        setRemote(flags)
    }

    public func setLocalOverride(_ value: Bool, for key: String) throws {
        guard hasLoadedRemote else { throw FeatureFlagOverrideError.remoteFlagsNotLoaded }
        guard remote[key] != nil else { throw FeatureFlagOverrideError.unknownFlag(key) }
        try store.set(value, for: key)
        localOverrides[key] = value
        rebuildEffectiveFlags()
    }

    public func removeLocalOverride(for key: String) throws {
        guard hasLoadedRemote else { throw FeatureFlagOverrideError.remoteFlagsNotLoaded }
        guard remote[key] != nil else { throw FeatureFlagOverrideError.unknownFlag(key) }
        try store.remove(key)
        localOverrides.removeValue(forKey: key)
        rebuildEffectiveFlags()
    }

    public func clearLocalOverrides() throws {
        try store.clear()
        localOverrides.removeAll()
        rebuildEffectiveFlags()
    }

    public func localOverride(for key: String) -> Bool? {
        guard remote[key] != nil else { return nil }
        return localOverrides[key]
    }

    private func rebuildEffectiveFlags() {
        guard hasLoadedRemote else {
            effective = [:]
            return
        }
        effective = remote.merging(localOverrides.filter { remote[$0.key] != nil }) { _, local in local }
    }

    private func advanceAuthoritativeGeneration() {
        authoritativeGeneration &+= 1
        latestAppliedPublicFetchSequence = 0
    }

    private func setRemote(_ flags: [String: Bool]) {
        remote = flags
        hasLoadedRemote = true
        rebuildEffectiveFlags()
    }
}

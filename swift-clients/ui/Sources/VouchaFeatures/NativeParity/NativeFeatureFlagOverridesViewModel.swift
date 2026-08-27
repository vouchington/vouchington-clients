import Observation
import VouchaAPI
import VouchaLocalization
import VouchaModels

@MainActor
@Observable
public final class NativeFeatureFlagOverridesViewModel {
    public private(set) var isLoading = false
    public private(set) var errorMessage: UiVerbatimText?
    let featureFlags: FeatureFlagState
    let canOverride: Bool
    private let client: APIClient?

    public init(client: APIClient?, featureFlags: FeatureFlagState, canOverride: Bool) {
        self.client = client
        self.featureFlags = featureFlags
        self.canOverride = canOverride
    }

    public func load() async {
        guard canOverride, let client else { return }
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }
        do {
            let fetchToken = featureFlags.beginPublicFetch()
            let response: FeatureFlagsResponse = try await client.send(.featureFlags)
            featureFlags.applyPublicResponse(response, for: fetchToken)
        } catch {
            errorMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsLoadFlagsFailed)
        }
    }

    func apply(_ choice: NativeFeatureFlagOverrideChoice, to key: String) {
        guard canOverride else { return }
        errorMessage = nil
        do {
            switch choice {
            case .inherited: try featureFlags.removeLocalOverride(for: key)
            case .enabled: try featureFlags.setLocalOverride(true, for: key)
            case .disabled: try featureFlags.setLocalOverride(false, for: key)
            }
        } catch {
            errorMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsSaveOverrideFailed)
        }
    }

    func clearAll() {
        guard canOverride else { return }
        errorMessage = nil
        do {
            try featureFlags.clearLocalOverrides()
        } catch {
            errorMessage = .message(.nativeSwiftDynamicConfigFeatureFlagsClearOverridesFailed)
        }
    }
}

enum NativeFeatureFlagOverrideChoice: Hashable {
    case inherited, enabled, disabled

    init(_ value: Bool?) {
        switch value {
        case true: self = .enabled
        case false: self = .disabled
        case nil: self = .inherited
        }
    }
}

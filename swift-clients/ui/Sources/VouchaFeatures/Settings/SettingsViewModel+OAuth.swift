import Foundation
import VouchaAuth
import VouchaCore
import VouchaModels

public extension SettingsViewModel {
    func loadOAuthCapabilities() async {
        guard let coordinator = nativeOAuthAuthorizationCoordinator else { return }
        await coordinator.loadCapabilities()
        synchronizeOAuthError()
    }

    func beginOAuthConnection(
        provider: NativeOAuthProvider,
        openAuthorizationURL: @escaping @MainActor (URL) -> Void
    ) async {
        guard let coordinator = nativeOAuthAuthorizationCoordinator else { return }
        oauthProviderInFlight = provider
        oauthErrorMessage = nil
        await coordinator.begin(
            provider: provider,
            purpose: .connect,
            openAuthorizationURL: openAuthorizationURL
        )
        if let error = coordinator.errorMessage {
            oauthErrorMessage = .verbatim(error)
        }
        oauthProviderInFlight = nil
    }

    func consumeOAuthAuthorizationResult(_ result: NativeOAuthAuthorizationResult?) async {
        switch result {
        case let .connected(provider):
            guard oauthProviderInFlight == nil else { return }
            oauthProviderInFlight = provider
            oauthErrorMessage = nil
            defer { oauthProviderInFlight = nil }
            do {
                try await refreshIdentity(confirmingConnectionTo: provider)
                nativeOAuthAuthorizationCoordinator?.acknowledgeResult()
            } catch {
                oauthErrorMessage = .verbatim(error.localizedDescription)
            }
        case .expired(_, .connect):
            oauthErrorMessage = .message(.nativeSwiftSettingsOauthExpired)
        case .authenticated, .mfaRequired, .expired(_, .authenticate), nil:
            break
        }
    }

    func synchronizeOAuthError() {
        guard let coordinator = nativeOAuthAuthorizationCoordinator else { return }
        if canDismissOAuthExpiration {
            oauthErrorMessage = .message(.nativeSwiftSettingsOauthExpired)
        } else if coordinator.canRecoverFailedFinalization,
                  let error = coordinator.errorMessage {
            oauthErrorMessage = .verbatim(error)
        } else if let error = coordinator.capabilityErrorMessage {
            oauthErrorMessage = .verbatim(error)
        }
    }

    func retryOAuthFinalization() async {
        guard let coordinator = nativeOAuthAuthorizationCoordinator else { return }
        oauthErrorMessage = nil
        await coordinator.retryFailedFinalization()
        synchronizeOAuthError()
    }

    func retryOAuthResultConsumption() async {
        guard canRetryOAuthResultConsumption else { return }
        if canRecoverOAuthFinalization {
            await retryOAuthFinalization()
        } else {
            await consumeOAuthAuthorizationResult(nativeOAuthAuthorizationCoordinator?.result)
        }
    }

    func cancelOAuthResultConsumption() {
        guard canRetryOAuthResultConsumption else { return }
        nativeOAuthAuthorizationCoordinator?.acknowledgeResult()
        oauthErrorMessage = nil
        synchronizeOAuthError()
    }

    func retryOAuthCapabilityLoading() async {
        oauthErrorMessage = nil
        await loadOAuthCapabilities()
    }

    func cancelOAuthAuthorization() {
        nativeOAuthAuthorizationCoordinator?.cancelPendingAuthorization()
        oauthErrorMessage = nil
    }

    func dismissOAuthExpiration() {
        guard canDismissOAuthExpiration else { return }
        nativeOAuthAuthorizationCoordinator?.acknowledgeResult()
        oauthErrorMessage = nil
        synchronizeOAuthError()
    }

    func disconnectOAuthAccount(provider: NativeOAuthProvider) async {
        guard let client else { return }
        oauthProviderInFlight = provider
        oauthErrorMessage = nil
        do {
            let _: EmptyResponse = try await client.send(.disconnectOAuthAccount(provider: provider))
            let response: SettingsIdentityResponse = try await client.send(.myIdentity)
            guard account(for: provider, in: response.identity) == nil else {
                throw NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed
            }
            apply(identity: response.identity)
        } catch {
            oauthErrorMessage = .verbatim(error.localizedDescription)
        }
        oauthProviderInFlight = nil
    }

    func account(for provider: NativeOAuthProvider) -> OAuthAccountInfo? {
        guard let identity else { return nil }
        return account(for: provider, in: identity)
    }

    private func account(
        for provider: NativeOAuthProvider,
        in identity: PrivateUser
    ) -> OAuthAccountInfo? {
        switch provider {
        case .facebook: identity.facebookAccount
        case .x: identity.xAccount
        case .github: identity.githubAccount
        }
    }

    private func refreshIdentity(confirmingConnectionTo provider: NativeOAuthProvider) async throws {
        guard let client else {
            throw NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed
        }
        let response: SettingsIdentityResponse = try await client.send(.myIdentity)
        guard account(for: provider, in: response.identity) != nil else {
            throw NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed
        }
        apply(identity: response.identity)
    }

    func canConnectOAuthAccount(provider: NativeOAuthProvider) -> Bool {
        nativeOAuthAuthorizationCoordinator?.supports(provider: provider, purpose: .connect) == true &&
            nativeOAuthAuthorizationCoordinator?.canStartAuthorization == true &&
            oauthProviderInFlight == nil
    }

    var canRetryOAuthResultConsumption: Bool {
        guard case .connected = nativeOAuthAuthorizationCoordinator?.result else { return false }
        return oauthErrorMessage != nil && oauthProviderInFlight == nil
    }

    var canRecoverOAuthFinalization: Bool {
        nativeOAuthAuthorizationCoordinator?.canRecoverFailedFinalization == true
    }

    var canRetryOAuthCapabilities: Bool {
        nativeOAuthAuthorizationCoordinator?.canRetryCapabilityLoading == true
    }

    var canDismissOAuthExpiration: Bool {
        guard case .expired(_, .connect) = nativeOAuthAuthorizationCoordinator?.result else {
            return false
        }
        return true
    }

    var canCancelOAuthAuthorization: Bool {
        nativeOAuthAuthorizationCoordinator?.canCancelPendingAuthorization == true
    }
}

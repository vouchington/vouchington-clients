import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

@Observable
@MainActor
public final class NativeOAuthAuthorizationCoordinator {
    public internal(set) var capabilities: OAuthBrokerCapabilities?
    public internal(set) var pending: PendingNativeOAuthAuthorization?
    public internal(set) var result: NativeOAuthAuthorizationResult?
    public internal(set) var isWorking = false
    public internal(set) var errorMessage: String?
    public internal(set) var capabilityErrorMessage: String?
    public internal(set) var isLoadingCapabilities = false

    let client: APIClient
    let sessionManager: SessionManager
    let store: NativeOAuthAuthorizationStore
    let now: @Sendable () -> Date
    var retainedCallbackURL: URL?
    var secureStateReadUnavailable = false
    var resultAcknowledgementNeedsRetry = false

    public init(
        client: APIClient,
        sessionManager: SessionManager,
        store: NativeOAuthAuthorizationStore,
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.client = client
        self.sessionManager = sessionManager
        self.store = store
        self.now = now
        synchronizeFromStore()
    }

    public func loadCapabilities() async {
        guard !isLoadingCapabilities else { return }
        isLoadingCapabilities = true
        defer { isLoadingCapabilities = false }
        do {
            let response: OAuthBrokerCapabilitiesResponse = try await client.send(.oauthProviders)
            capabilities = response.brokerCapabilities
            capabilityErrorMessage = nil
        } catch {
            capabilityErrorMessage = error.localizedDescription
        }
    }

    public var canRetryCapabilityLoading: Bool {
        capabilities == nil && capabilityErrorMessage != nil && !isLoadingCapabilities
    }

    public func supports(
        provider: NativeOAuthProvider,
        purpose: NativeOAuthAuthorizationPurpose
    ) -> Bool {
        capabilities?[provider].supportsNative(purpose) == true
    }

    public func begin(
        provider: NativeOAuthProvider,
        purpose: NativeOAuthAuthorizationPurpose,
        openAuthorizationURL: @escaping @MainActor (URL) -> Void
    ) async {
        guard canBeginAuthorization else { return }
        if capabilities == nil {
            await loadCapabilities()
        }
        guard canBeginAuthorization,
              supports(provider: provider, purpose: purpose)
        else { return }
        isWorking = true
        errorMessage = nil
        defer { isWorking = false }
        do {
            let proof = try NativeAuthorizationCompletionProof.generate()
            let response: BeginNativeOAuthAuthorizationResponse = try await client.send(
                .beginNativeOAuthAuthorization(
                    provider: provider,
                    purpose: purpose,
                    completionProofChallenge: proof.challenge
                )
            )
            guard hasNoAuthorizationState else { return }
            guard let url = URL(string: response.redirectUrl),
                  url.scheme?.lowercased() == "https",
                  response.expiresAt > now(),
                  store.save(
                      flowId: response.flowId,
                      provider: provider,
                      purpose: purpose,
                      completionProofVerifier: proof.verifier,
                      expiresAt: response.expiresAt,
                      now: now()
                  )
            else {
                throw NativeOAuthAuthorizationCoordinatorError.invalidAuthorization
            }
            synchronizeFromStore()
            openAuthorizationURL(url)
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private var canBeginAuthorization: Bool {
        !isWorking && hasNoAuthorizationState
    }

    public var canStartAuthorization: Bool {
        canBeginAuthorization
    }

    private var hasNoAuthorizationState: Bool {
        pending == nil &&
            result == nil &&
            retainedCallbackURL == nil &&
            !secureStateReadUnavailable &&
            !resultAcknowledgementNeedsRetry
    }

    public func cancelPendingAuthorization() {
        guard !isWorking else { return }
        do {
            try store.clearPending()
            retainedCallbackURL = nil
            errorMessage = nil
            synchronizeFromStore()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    public func discardAuthorizationAfterSuccessfulSignIn() {
        do {
            try store.discardAuthorization()
            retainedCallbackURL = nil
            resultAcknowledgementNeedsRetry = false
            errorMessage = nil
            synchronizeFromStore()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    public var canCancelPendingAuthorization: Bool {
        pending?.isFinalizing == false && !isWorking
    }

    public func acknowledgeResult() {
        do {
            try store.acknowledgeResult()
            resultAcknowledgementNeedsRetry = false
            errorMessage = nil
            synchronizeFromStore()
        } catch {
            resultAcknowledgementNeedsRetry = true
            errorMessage = error.localizedDescription
        }
    }

}

public enum NativeOAuthAuthorizationCoordinatorError: LocalizedError {
    case invalidAuthorization
    case identityRefreshFailed

    public var errorDescription: String? {
        switch self {
        case .invalidAuthorization:
            "The authorization response was invalid."
        case .identityRefreshFailed:
            "The account could not be confirmed."
        }
    }
}

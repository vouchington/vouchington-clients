import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension NativeOAuthAuthorizationCoordinator {
    public func cancelFreshAccountConfirmation() {
        freshConfirmationEpoch &+= 1
        let hasConfirmationResult = switch result {
        case .confirmationReady, .expired(_, .reauthenticate): true
        default: false
        }
        guard pending?.purpose == .reauthenticate || hasConfirmationResult else { return }
        do {
            try store.discardAuthorization()
            retainedCallbackURL = nil
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

    /// Drops a restored or late fresh-confirmation result when no confirmation view model is waiting for it.
    public func discardFreshConfirmationWithoutOwner() {
        guard !hasFreshConfirmationOwner else { return }
        switch result {
        case .confirmationReady, .expired(_, .reauthenticate): cancelFreshAccountConfirmation()
        default: break
        }
    }

    func finalize(_ pending: PendingNativeOAuthAuthorization) async {
        guard let completionToken = pending.completionToken,
              acquireFinalizationLease(for: pending)
        else { return }
        isWorking = true
        errorMessage = nil
        defer {
            store.releaseFinalizationLease(flowId: pending.flowId)
            isWorking = false
            synchronizeFromStore()
        }

        do {
            while now() < pending.expiresAt {
                let response: NativeOAuthCompletionResponse = try await client.send(
                    .completeNativeOAuthAuthorization(
                        flowId: pending.flowId,
                        completionToken: completionToken,
                        completionProofVerifier: pending.completionProofVerifier
                    )
                )
                if try await handleCompletion(response, pending: pending) { return }
                try await Task.sleep(for: .seconds(1))
            }
            persistExpiration(for: pending)
        } catch is CancellationError {
            return
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private func handleCompletion(
        _ response: NativeOAuthCompletionResponse, pending: PendingNativeOAuthAuthorization
    ) async throws -> Bool {
        if pending.purpose == .reauthenticate {
            return try handleConfirmationCompletion(response, pending: pending)
        }
        switch response {
        case .pending:
            return false
        case .authenticated:
            guard await sessionManager.refresh() else {
                throw NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed
            }
            try persistResult(.authenticated(provider: pending.provider), for: pending)
        case let .mfaRequired(loginAttemptId):
            try persistResult(.mfaRequired(provider: pending.provider, loginAttemptId: loginAttemptId), for: pending)
        case .connected:
            guard await confirmConnectedProvider(pending.provider) else {
                throw NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed
            }
            try persistResult(.connected(provider: pending.provider), for: pending)
        }
        return true
    }

    private func handleConfirmationCompletion(
        _ response: NativeOAuthCompletionResponse, pending: PendingNativeOAuthAuthorization
    ) throws -> Bool {
        switch response {
        case .pending: return false
        case .connected:
            try persistResult(.confirmationReady(provider: pending.provider, flowId: pending.flowId), for: pending)
            return true
        case .authenticated, .mfaRequired:
            throw NativeOAuthAuthorizationCoordinatorError.invalidAuthorization
        }
    }

    private func persistExpiration(for pending: PendingNativeOAuthAuthorization) {
        _ = store.complete(
            pending,
            with: .expired(provider: pending.provider, purpose: pending.purpose)
        )
    }

    private func persistResult(
        _ result: NativeOAuthAuthorizationResult,
        for pending: PendingNativeOAuthAuthorization
    ) throws {
        guard store.complete(pending, with: result) else {
            throw NativeOAuthSecureStateError.unavailable
        }
    }

    private func acquireFinalizationLease(
        for pending: PendingNativeOAuthAuthorization
    ) -> Bool {
        do {
            return try store.acquireFinalizationLease(for: pending, now: now())
        } catch {
            secureStateReadUnavailable = true
            errorMessage = error.localizedDescription
            return false
        }
    }

    func confirmConnectedProvider(_ provider: NativeOAuthProvider) async -> Bool {
        struct IdentityEnvelope: Decodable {
            let identity: PrivateUser
        }
        do {
            let response: IdentityEnvelope = try await client.send(.myIdentity)
            let isConnected = switch provider {
            case .facebook: response.identity.facebookAccount != nil
            case .x: response.identity.xAccount != nil
            case .github: response.identity.githubAccount != nil
            case .linkedin: response.identity.linkedinAccount != nil
            case .microsoft: response.identity.microsoftAccount != nil
            case .google: response.identity.googleAccount != nil
            }
            guard isConnected else { return false }
            sessionManager.synchronize(with: response.identity)
            return true
        } catch {
            return false
        }
    }

    func synchronizeFromStore() {
        do {
            let snapshot = try store.snapshot(now: now())
            apply(snapshot)
            if case let .expired(pending) = snapshot.pendingStatus {
                try store.record(.expired(
                    provider: pending.provider,
                    purpose: pending.purpose
                ))
                try apply(store.snapshot(now: now()))
            }
            clearSecureStateReadError()
        } catch {
            secureStateReadUnavailable = true
            errorMessage = error.localizedDescription
        }
    }

    func apply(_ snapshot: NativeOAuthAuthorizationSnapshot) {
        switch snapshot.pendingStatus {
        case let .active(storedPending), let .expired(storedPending):
            pending = storedPending
        case .none:
            pending = nil
        }
        result = snapshot.result
    }

    func clearSecureStateReadError() {
        if secureStateReadUnavailable {
            secureStateReadUnavailable = false
            if errorMessage == NativeOAuthSecureStateError.unavailable.localizedDescription {
                errorMessage = nil
            }
        }
    }
}

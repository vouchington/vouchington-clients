import Foundation
import VouchaCore
import VouchaLocalization

struct PendingApiKeySecret {
    let ownerId: String
    let rawKey: String
    let operationGeneration: Int
}

struct PendingApiKeyCreation {
    let ownerId: String
    let response: SettingsApiKeyResponse
    let operationGeneration: Int
}

struct ApiKeyRotationOwnerState {
    var pendingSecret: PendingApiKeySecret?
    var pendingCreation: PendingApiKeyCreation?
    var invalidationGeneration = 0
    var secretOperationGeneration = 0
    var lastConfirmedOwnerId: String?
    var identityConfirmed = false
}

struct ApiKeyRotationContext {
    let ownerId: String
    let loadGeneration: Int
    let invalidationGeneration: Int
    let secretOperationGeneration: Int
}

extension SettingsViewModel {
    func beginApiKeySecretOperation() -> Int {
        apiKeyRotationOwnerState.secretOperationGeneration += 1
        apiKeyRotationOwnerState.pendingSecret = nil
        apiKeyRotationOwnerState.pendingCreation = nil
        latestRawAPIKey = nil
        return apiKeyRotationOwnerState.secretOperationGeneration
    }

    func publishOrHoldApiKeyRotationSecret(
        _ rawKey: String,
        ownerId: String,
        invalidationGeneration: Int,
        secretOperationGeneration: Int
    ) -> Bool {
        guard invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration,
              secretOperationGeneration == apiKeyRotationOwnerState.secretOperationGeneration else { return false }
        guard let currentOwnerId = identity?.id else {
            apiKeyRotationOwnerState.pendingSecret = PendingApiKeySecret(
                ownerId: ownerId, rawKey: rawKey, operationGeneration: secretOperationGeneration
            )
            return false
        }
        guard currentOwnerId == ownerId else {
            apiKeyRotationOwnerState.pendingSecret = nil
            return false
        }
        latestRawAPIKey = rawKey
        return true
    }

    func confirmApiKeyRotationOwner(_ ownerId: String) {
        apiKeyRotationOwnerState.identityConfirmed = true
        apiKeyRotationOwnerState.lastConfirmedOwnerId = ownerId
        if let pendingCreation = apiKeyRotationOwnerState.pendingCreation {
            apiKeyRotationOwnerState.pendingCreation = nil
            if pendingCreation.ownerId == ownerId,
               pendingCreation.operationGeneration == apiKeyRotationOwnerState.secretOperationGeneration {
                reconcileCreatedApiKey(pendingCreation.response, preserveDuringMainLoad: true)
            }
        }
        guard let pending = apiKeyRotationOwnerState.pendingSecret else { return }
        apiKeyRotationOwnerState.pendingSecret = nil
        guard pending.ownerId == ownerId,
              pending.operationGeneration == apiKeyRotationOwnerState.secretOperationGeneration else { return }
        latestRawAPIKey = pending.rawKey
        statusMessage = .message(.nativeApiKeysRotated)
    }

    func invalidateApiKeyRotationOwner() {
        apiKeyRotationOwnerState.invalidationGeneration += 1
        apiKeyRotationInFlight.removeAll()
        _ = beginApiKeySecretOperation()
        apiKeyRotationOwnerState.lastConfirmedOwnerId = nil
        apiKeyRotationOwnerState.identityConfirmed = false
    }

    func invalidateApiKeyRotationOwner(ifGenerationMatches generation: Int) {
        guard generation == apiKeyRotationOwnerState.invalidationGeneration else { return }
        invalidateApiKeyRotationOwner()
    }

    func failCurrentApiKeyCredentialAsUnauthorized(ifGenerationMatches generation: Int) {
        guard generation == apiKeyRotationOwnerState.invalidationGeneration else { return }
        invalidateApiKeyRotationOwner()
        state = .error(.unauthorized)
        statusMessage = VouchaError.unauthorized.errorDescription.map(UiVerbatimText.verbatim)
    }

    func reconcileCreatedApiKey(_ response: SettingsApiKeyResponse, preserveDuringMainLoad: Bool) {
        if preserveDuringMainLoad { createdApiKeysDuringMainLoad.append(response.apiKey) }
        revokedApiKeyIds.remove(response.apiKey.id)
        apiKeyPagination.invalidateRequestsPreservingPage()
        apiKeyPagination.replaceItems([response.apiKey] + apiKeyPagination.items)
        latestRawAPIKey = response.rawKey
        apiKeyLabel = ""
        apiKeyScopeSelection.clear()
        statusMessage = .message(.nativeSwiftSettingsApiKeyCreated)
    }

    func handleApiKeyCreationFailure(
        _ error: Error,
        ownerId: String,
        invalidationGeneration: Int,
        secretOperationGeneration: Int
    ) {
        guard secretOperationGeneration == apiKeyRotationOwnerState.secretOperationGeneration else { return }
        if let apiError = error as? VouchaError, case .unauthorized = apiError {
            failCurrentApiKeyCredentialAsUnauthorized(ifGenerationMatches: invalidationGeneration)
            return
        }
        guard invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration,
              identity?.id == ownerId else { return }
        let apiError = (error as? VouchaError) ?? .unexpected(error.localizedDescription)
        statusMessage = apiError.errorDescription.map(UiVerbatimText.verbatim)
        state = .error(apiError)
    }
}

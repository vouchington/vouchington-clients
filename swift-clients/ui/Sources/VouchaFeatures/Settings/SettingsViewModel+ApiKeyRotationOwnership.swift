import Foundation
import VouchaCore
import VouchaLocalization

struct PendingApiKeySecret {
    let ownerId: String
    let rawKey: String
    let operationGeneration: Int
    let statusMessage: UiVerbatimText?
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
    public func dismissRawApiKey() {
        guard latestRawAPIKey != nil else { return }
        latestRawAPIKey = nil
        if apiKeyRotationOwnerState.pendingSecret?.operationGeneration == apiKeyRotationOwnerState
            .secretOperationGeneration {
            apiKeyRotationOwnerState.pendingSecret = nil
            apiKeySecretOperationInFlight = false
        }
        if apiKeyRotationOwnerState.pendingCreation?.operationGeneration == apiKeyRotationOwnerState
            .secretOperationGeneration {
            apiKeyRotationOwnerState.pendingCreation = nil
            apiKeySecretOperationInFlight = false
        }
    }

    func beginApiKeySecretOperation() -> Int {
        apiKeyRotationOwnerState.secretOperationGeneration += 1
        apiKeyRotationOwnerState.pendingSecret = nil
        apiKeyRotationOwnerState.pendingCreation = nil
        latestRawAPIKey = nil
        return apiKeyRotationOwnerState.secretOperationGeneration
    }

    func finishApiKeySecretOperation(invalidationGeneration: Int, secretOperationGeneration: Int) {
        guard invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration,
              secretOperationGeneration == apiKeyRotationOwnerState.secretOperationGeneration else { return }
        guard apiKeyRotationOwnerState.pendingSecret?.operationGeneration != secretOperationGeneration,
              apiKeyRotationOwnerState.pendingCreation?.operationGeneration != secretOperationGeneration else { return }
        apiKeySecretOperationInFlight = false
    }

    func holdDisplayedApiKeySecretForReload() {
        guard let rawKey = latestRawAPIKey else { return }
        defer { latestRawAPIKey = nil }
        guard apiKeyRotationOwnerState.identityConfirmed,
              let ownerId = identity?.id else { return }
        apiKeyRotationOwnerState.pendingSecret = PendingApiKeySecret(
            ownerId: ownerId,
            rawKey: rawKey,
            operationGeneration: apiKeyRotationOwnerState.secretOperationGeneration,
            statusMessage: statusMessage
        )
        apiKeySecretOperationInFlight = true
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
                ownerId: ownerId,
                rawKey: rawKey,
                operationGeneration: secretOperationGeneration,
                statusMessage: .message(.nativeApiKeysRotated)
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
                apiKeySecretOperationInFlight = false
            }
        }
        guard let pending = apiKeyRotationOwnerState.pendingSecret else { return }
        apiKeyRotationOwnerState.pendingSecret = nil
        guard pending.ownerId == ownerId,
              pending.operationGeneration == apiKeyRotationOwnerState.secretOperationGeneration else { return }
        latestRawAPIKey = pending.rawKey
        statusMessage = pending.statusMessage
        apiKeySecretOperationInFlight = false
    }

    func invalidateApiKeyRotationOwner() {
        apiKeyRotationOwnerState.invalidationGeneration += 1
        apiKeyRotationInFlight.removeAll()
        apiKeyCreationInFlight = false
        apiKeySecretOperationInFlight = false
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

    func handleApiKeyCreationSuccess(
        _ response: SettingsApiKeyResponse,
        ownerId: String,
        invalidationGeneration: Int,
        secretOperationGeneration: Int
    ) {
        guard invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration,
              secretOperationGeneration == apiKeyRotationOwnerState.secretOperationGeneration else { return }
        guard identity?.id == ownerId else {
            if identity == nil, apiKeyRotationOwnerState.lastConfirmedOwnerId == ownerId {
                apiKeyRotationOwnerState.pendingCreation = PendingApiKeyCreation(
                    ownerId: ownerId, response: response, operationGeneration: secretOperationGeneration
                )
            }
            return
        }
        reconcileCreatedApiKey(
            response,
            preserveDuringMainLoad: activeMainSettingsLoadGeneration == settingsLoadGeneration
        )
        state = .loaded
        if activeMainSettingsLoadGeneration == settingsLoadGeneration { state = .loading }
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

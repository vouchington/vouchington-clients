import VouchaLocalization

struct ApiKeyRotationOwnerState {
    var pendingSecret: (ownerId: String, rawKey: String)?
    var invalidationGeneration = 0
    var lastConfirmedOwnerId: String?
    var identityConfirmed = false
}

struct ApiKeyRotationContext {
    let ownerId: String
    let loadGeneration: Int
    let invalidationGeneration: Int
}

extension SettingsViewModel {
    func publishOrHoldApiKeyRotationSecret(
        _ rawKey: String,
        ownerId: String,
        invalidationGeneration: Int
    ) -> Bool {
        guard invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration else { return false }
        guard let currentOwnerId = identity?.id else {
            apiKeyRotationOwnerState.pendingSecret = (ownerId, rawKey)
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
        guard let pending = apiKeyRotationOwnerState.pendingSecret else { return }
        apiKeyRotationOwnerState.pendingSecret = nil
        guard pending.ownerId == ownerId else { return }
        latestRawAPIKey = pending.rawKey
        statusMessage = .message(.nativeApiKeysRotated)
    }

    func invalidateApiKeyRotationOwner() {
        apiKeyRotationOwnerState.invalidationGeneration += 1
        apiKeyRotationOwnerState.pendingSecret = nil
        latestRawAPIKey = nil
        apiKeyRotationOwnerState.lastConfirmedOwnerId = nil
        apiKeyRotationOwnerState.identityConfirmed = false
    }

    func invalidateApiKeyRotationOwner(ifGenerationMatches generation: Int) {
        guard generation == apiKeyRotationOwnerState.invalidationGeneration else { return }
        invalidateApiKeyRotationOwner()
    }
}

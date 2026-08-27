import Foundation

extension LocalLLMSettingsStore {
    func scheduleLegacySecretMigration() {
        Task { [weak self] in
            await self?.migrateLegacySecret()
        }
    }

    func migrateLegacySecret() async {
        await enqueueVoid { [self] in
            guard load().endpoint(id: Self.legacyEndpointID) != nil else { return }
            try? await secretStore.migrateLegacyAPIKey(to: Self.legacyEndpointID)
        }
    }

    func clearLegacyAPIKeyIfNeeded(for endpointID: UUID) async throws {
        if endpointID == Self.legacyEndpointID {
            try await secretStore.clearLegacyAPIKey()
        }
    }
}

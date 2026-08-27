import Foundation

public extension InMemoryLocalLLMSecretStore {
    func migrateLegacyAPIKey(to endpointID: UUID) async {
        guard let legacyAPIKey else { return }
        if apiKeys[endpointID] == nil {
            apiKeys[endpointID] = legacyAPIKey
        }
        self.legacyAPIKey = nil
    }

    func readLegacyAPIKey() -> String? {
        legacyAPIKey
    }

    func clearLegacyAPIKey() async {
        legacyAPIKey = nil
    }
}

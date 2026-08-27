import Foundation

public extension LocalLLMSettingsStore {
    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async -> Bool {
        await enqueue { [self] in
            let trimmed = apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
            do {
                if trimmed.isEmpty {
                    try await clearSecret(for: endpointID)
                } else {
                    try await secretStore.saveAPIKey(trimmed, for: endpointID)
                }
                return true
            } catch {
                return false
            }
        }
    }

    func clearAPIKey(for endpointID: UUID) async -> Bool {
        await enqueue { [self] in
            do {
                try await clearSecret(for: endpointID)
                return true
            } catch {
                return false
            }
        }
    }
}

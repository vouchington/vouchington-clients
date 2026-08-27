import Foundation

extension LocalLLMSettingsStore {
    func persist(
        _ configuration: LocalLLMConfiguration,
        apiKeys: [UUID: String],
        previous: LocalLLMConfiguration? = nil
    ) async -> Bool {
        var configuration = configuration
        var seenEndpointIDs = Set<UUID>()
        configuration.endpoints = Array(configuration.endpoints.reversed().filter { endpoint in
            seenEndpointIDs.insert(endpoint.id).inserted
        }.reversed())
        let previous = previous ?? load()
        let newProfiles = Dictionary(uniqueKeysWithValues: configuration.endpoints.map { ($0.id, $0) })
        let originChangedIDs = Set<UUID>(previous.endpoints.compactMap { oldProfile in
            guard let newProfile = newProfiles[oldProfile.id], oldProfile.origin != newProfile.origin else {
                return nil
            }
            return oldProfile.id
        })
        let removedIDs = Set(previous.endpoints.compactMap { oldProfile in
            newProfiles[oldProfile.id] == nil ? oldProfile.id : nil
        })
        let secretCleanupIDs = originChangedIDs.union(removedIDs)
        let credentialStagingIDs = Set<UUID>(configuration.endpoints.compactMap { profile in
            guard profile.isEnabled, apiKeys.keys.contains(profile.id) else { return nil }
            guard let oldProfile = previous.endpoint(id: profile.id) else {
                let apiKey = apiKeys[profile.id]?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
                return apiKey.isEmpty ? nil : profile.id
            }
            return oldProfile.isEnabled ? nil : profile.id
        })
        let stagingIDs = secretCleanupIDs.union(credentialStagingIDs)
        if stagingIDs.isEmpty {
            guard write(configuration) else { return false }
        } else {
            let removedProfiles = previous.endpoints.filter { removedIDs.contains($0.id) }
            guard write(configuration.safeBeforeSecretReplacement(
                for: stagingIDs,
                retaining: removedProfiles
            )) else { return false }
        }
        let scopedAPIKeys = apiKeys.filter { newProfiles[$0.key] != nil }
        guard await clearSecrets(for: secretCleanupIDs),
              await persistAPIKeys(scopedAPIKeys)
        else { return false }
        if !stagingIDs.isEmpty {
            guard write(configuration) else { return false }
        }
        return true
    }

    func clearSecret(for endpointID: UUID) async throws {
        try await secretStore.clearAPIKey(for: endpointID)
        try await clearLegacyAPIKeyIfNeeded(for: endpointID)
    }

    private func clearSecrets(for endpointIDs: Set<UUID>) async -> Bool {
        do {
            for endpointID in endpointIDs {
                try await clearSecret(for: endpointID)
            }
            return true
        } catch {
            return false
        }
    }

    private func persistAPIKeys(_ apiKeys: [UUID: String]) async -> Bool {
        do {
            for (endpointID, apiKey) in apiKeys {
                let trimmed = apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
                if trimmed.isEmpty {
                    try await clearSecret(for: endpointID)
                } else {
                    try await secretStore.saveAPIKey(trimmed, for: endpointID)
                }
            }
            return true
        } catch {
            return false
        }
    }
}

extension LocalLLMConfiguration {
    func safeBeforeSecretReplacement(
        for endpointIDs: Set<UUID>,
        retaining removedProfiles: [LocalLLMEndpointProfile] = []
    ) -> Self {
        var configuration = self
        configuration.endpoints.append(contentsOf: removedProfiles)
        configuration.endpoints = configuration.endpoints.map { endpoint in
            guard endpointIDs.contains(endpoint.id) else { return endpoint }
            var endpoint = endpoint
            endpoint.isEnabled = false
            return endpoint
        }
        if let selectedEndpointID = configuration.selectedEndpointID,
           endpointIDs.contains(selectedEndpointID) {
            configuration.selectedEndpointID = nil
        }
        if let selectedProviderID = configuration.selectedProviderID,
           endpointIDs.contains(where: {
               selectedProviderID == "openai_compatible:\($0.uuidString.lowercased())"
           }) {
            configuration.selectedProviderID = nil
        }
        return configuration
    }
}

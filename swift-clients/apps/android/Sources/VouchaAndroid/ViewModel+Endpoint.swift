import Foundation
import VouchaCore

extension ViewModel {
    var isSelectedProviderAvailable: Bool {
        switch selectedProvider {
        case .aiCore:
            modelState == .available
        case .endpoint:
            endpointProfile?.isEnabled == true
                && endpointProfile?.responsesURL != nil
                && endpointProfile?.selectedModel != nil
        }
    }

    func selectProvider(_ provider: AndroidChatProvider) async {
        guard provider != .endpoint || draftEndpointProfile != nil else {
            return
        }
        do {
            selectedProvider = provider
            if provider == .endpoint {
                try await saveEndpointSettings()
            } else {
                let selectedProviderID = provider.rawValue
                guard await settingsStore.update({ configuration in
                    configuration.selectedProviderID = selectedProviderID
                }) else {
                    throw AndroidAICoreError.endpointSaveFailed
                }
            }
        } catch {
            selectedProvider = settingsStore.load().selectedProviderID == AndroidChatProvider.endpoint.rawValue
                ? .endpoint
                : .aiCore
            errorMessage = error.localizedDescription
        }
    }

    func saveEndpointSettings() async throws {
        let profile = LocalLLMEndpointProfile(
            id: endpointProfileID,
            displayName: "Android endpoint",
            isEnabled: true,
            endpoint: endpoint,
            modelNames: [endpointModel],
            selectedModelName: endpointModel
        )
        guard profile.responsesURL != nil, profile.selectedModel != nil else {
            throw LocalLLMError.invalidEndpoint
        }
        let oldProfile = settingsStore.load().endpoint(id: profile.id)
        let requiresSecretRotation = oldProfile != nil
            && (oldProfile?.origin != profile.origin || oldProfile?.isEnabled == false)
        let apiKey = endpointAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        let shouldClearSecret = apiKey.isEmpty || (requiresSecretRotation && !endpointAPIKeyWasExplicitlyEdited)
        let selectedProviderID = selectedProvider.rawValue
        if requiresSecretRotation {
            try await rotateOriginSecret(
                profile: profile,
                selectedProviderID: selectedProviderID,
                apiKey: apiKey,
                shouldClearSecret: shouldClearSecret
            )
        } else if !shouldClearSecret, oldProfile == nil {
            try await stageNewEndpoint(profile: profile, selectedProviderID: selectedProviderID)
            try secretStore.set(apiKey, forKey: endpointSecretKey(for: profile.id))
            endpointAPIKey = apiKey
        }
        guard await settingsStore.update({ configuration in
            Self.replaceEndpoint(profile, in: &configuration)
            configuration.isEnabled = true
            configuration.selectedEndpointID = profile.id
            configuration.selectedProviderID = selectedProviderID
        }) else {
            throw AndroidAICoreError.endpointSaveFailed
        }
        if shouldClearSecret, !requiresSecretRotation {
            try secretStore.removeValue(forKey: endpointSecretKey(for: profile.id))
        } else if !shouldClearSecret, !requiresSecretRotation {
            try secretStore.set(apiKey, forKey: endpointSecretKey(for: profile.id))
        }
        endpointAPIKeyWasExplicitlyEdited = false
    }

    private func stageNewEndpoint(
        profile: LocalLLMEndpointProfile,
        selectedProviderID: String
    ) async throws {
        var tombstone = profile
        tombstone.isEnabled = false
        let stagingProfile = tombstone
        guard await settingsStore.update({ configuration in
            Self.replaceEndpoint(stagingProfile, in: &configuration)
            if configuration.selectedEndpointID == profile.id {
                configuration.selectedEndpointID = nil
            }
            if configuration.selectedProviderID == selectedProviderID {
                configuration.selectedProviderID = nil
            }
        }) else {
            throw AndroidAICoreError.endpointSaveFailed
        }
    }

    private func rotateOriginSecret(
        profile: LocalLLMEndpointProfile,
        selectedProviderID: String,
        apiKey: String,
        shouldClearSecret: Bool
    ) async throws {
        var disabledProfile = profile
        disabledProfile.isEnabled = false
        let tombstone = disabledProfile
        guard await settingsStore.update({ configuration in
            Self.replaceEndpoint(tombstone, in: &configuration)
            if configuration.selectedEndpointID == profile.id {
                configuration.selectedEndpointID = nil
            }
            if configuration.selectedProviderID == selectedProviderID {
                configuration.selectedProviderID = nil
            }
        }) else {
            throw AndroidAICoreError.endpointSaveFailed
        }
        if shouldClearSecret {
            endpointAPIKey = ""
            endpointAPIKeyWasExplicitlyEdited = false
        }
        try secretStore.removeValue(forKey: endpointSecretKey(for: profile.id))
        if !shouldClearSecret {
            try secretStore.set(apiKey, forKey: endpointSecretKey(for: profile.id))
            endpointAPIKey = apiKey
        }
    }

    private nonisolated static func replaceEndpoint(
        _ profile: LocalLLMEndpointProfile,
        in configuration: inout LocalLLMConfiguration
    ) {
        if let index = configuration.endpoints.firstIndex(where: { $0.id == profile.id }) {
            configuration.endpoints[index] = profile
        } else {
            configuration.endpoints.append(profile)
        }
    }

    var endpointProfile: LocalLLMEndpointProfile? {
        settingsStore.load().endpoint(id: endpointProfileID)
    }

    private var draftEndpointProfile: LocalLLMEndpointProfile? {
        let profile = LocalLLMEndpointProfile(
            id: endpointProfileID,
            displayName: "Android endpoint",
            isEnabled: true,
            endpoint: endpoint,
            modelNames: [endpointModel],
            selectedModelName: endpointModel
        )
        guard profile.responsesURL != nil, profile.selectedModel != nil else { return nil }
        return profile
    }

    func generateResponse(for message: String) async throws -> String {
        switch selectedProvider {
        case .aiCore:
            return try await runtime.generate(prompt: boundedPrompt(latest: message))
        case .endpoint:
            guard let endpoint = endpointProfile else { throw LocalLLMError.invalidEndpoint }
            let history = messages.dropLast().suffix(6).map {
                LocalLLMChatMessage(role: $0.role, content: String($0.content.prefix(1_000)))
            }
            let apiKey = try secretStore.string(forKey: endpointSecretKey(for: endpoint.id))
            return try await endpointClient.generateAssistantResponse(
                message: String(message.prefix(2_000)),
                history: history,
                endpoint: endpoint,
                apiKey: apiKey
            ) ?? ""
        }
    }

    func endpointSecretKey(for endpointID: UUID) -> String {
        "openai-compatible-api-key-\(endpointID.uuidString.lowercased())"
    }
}

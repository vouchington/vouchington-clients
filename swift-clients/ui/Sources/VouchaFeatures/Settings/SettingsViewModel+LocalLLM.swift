import Foundation
import VouchaCore
import VouchaLocalization

public extension SettingsViewModel {
    var localLLMSettingsAvailable: Bool {
        localLLMFeaturePolicy.isEnabled
    }

    var localLLMEndpoints: [LocalLLMEndpointProfile] {
        localLLMConfiguration.endpoints
    }

    var localLLMSelectedEndpointID: UUID? {
        localLLMConfiguration.selectedEndpointID
    }

    func isLocalLLMEndpointActive(id: UUID) -> Bool {
        localLLMConfiguration.selectedProviderID == "openai_compatible:\(id.uuidString.lowercased())"
    }

    func loadLocalLLMSettings() async {
        localLLMConfiguration = localLLMSettingsStore.load()
        localLLMEnabled = localLLMConfiguration.isEnabled
    }

    func setLocalLLMEnabled(_ enabled: Bool) async {
        let previousEnabled = localLLMEnabled
        localLLMEnabled = enabled
        let didSave = await localLLMSettingsStore.update(apiKeys: [:]) { configuration in
            configuration.isEnabled = enabled
            if !enabled, configuration.selectedProviderID?.hasPrefix("openai_compatible:") == true {
                configuration.selectedProviderID = nil
                configuration.selectedEndpointID = nil
            }
        }
        guard didSave else {
            localLLMEnabled = previousEnabled
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return
        }
        await loadLocalLLMSettings()
    }

    func createLocalLLMEndpoint() async {
        let submittedDraft = currentLocalLLMDraftSnapshot()
        let profile = buildLocalLLMEndpointProfile(id: localLLMDraftEndpointID, defaultToFirstModel: true)
        let apiKey = submittedDraft.apiKey.trimmingCharacters(in: .whitespacesAndNewlines)
        let didSave = await localLLMSettingsStore.update(apiKeys: [profile.id: apiKey]) { configuration in
            if let index = configuration.endpoints.firstIndex(where: { $0.id == profile.id }) {
                configuration.endpoints[index] = profile
            } else {
                configuration.endpoints.append(profile)
            }
            if configuration.selectedEndpointID == nil {
                configuration.selectedEndpointID = profile.id
            }
        }
        guard didSave else {
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return
        }
        if currentLocalLLMDraftSnapshot() == submittedDraft {
            resetLocalLLMDraft()
        } else {
            localLLMDraftEndpointID = UUID()
        }
        await loadLocalLLMSettings()
        localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaved)
    }

    private func currentLocalLLMDraftSnapshot() -> LocalLLMEndpointDraft {
        LocalLLMEndpointDraft(
            displayName: localLLMDisplayName,
            isEnabled: true,
            endpoint: localLLMEndpoint,
            modelsText: localLLMModelsText,
            selectedModel: localLLMSelectedModel,
            apiKey: localLLMAPIKey
        )
    }

    @discardableResult
    func updateLocalLLMEndpoint(
        id: UUID,
        draft: LocalLLMEndpointDraft,
        apiKeyWasEdited: Bool
    ) async -> Bool {
        guard localLLMSettingsStore.load().endpoint(id: id) != nil else {
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return false
        }
        let profile = LocalLLMEndpointProfile(
            id: id,
            displayName: draft.displayName.trimmingCharacters(in: .whitespacesAndNewlines),
            isEnabled: draft.isEnabled,
            endpoint: draft.endpoint,
            modelNames: normalizedModelNames(from: draft.modelsText),
            selectedModelName: draft.selectedModel.trimmingCharacters(in: .whitespacesAndNewlines)
        )
        let apiKeys = apiKeyWasEdited ? [profile.id: draft.apiKey] : [:]
        let didSave = await localLLMSettingsStore.update(apiKeys: apiKeys) { configuration in
            guard let index = configuration.endpoints.firstIndex(where: { $0.id == id }) else { return }
            configuration.endpoints[index] = profile
            if !profile.isSelectableAsCurrent,
               configuration.selectedProviderID == "openai_compatible:\(id.uuidString.lowercased())" {
                configuration.selectedProviderID = nil
                if configuration.selectedEndpointID == id {
                    configuration.selectedEndpointID = nil
                }
            }
        }
        guard didSave else {
            await loadLocalLLMSettings()
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return false
        }
        await loadLocalLLMSettings()
        guard localLLMConfiguration.endpoint(id: id) != nil else {
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return false
        }
        localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaved)
        return true
    }

    func deleteLocalLLMEndpoint(id: UUID) async {
        let didSave = await localLLMSettingsStore.update(apiKeys: [:]) { configuration in
            configuration.endpoints.removeAll { $0.id == id }
            if configuration.selectedEndpointID == id {
                configuration.selectedEndpointID = configuration.endpoints.first?.id
            }
            if configuration.selectedProviderID == "openai_compatible:\(id.uuidString.lowercased())" {
                configuration.selectedProviderID = nil
            }
        }
        guard didSave else {
            await loadLocalLLMSettings()
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return
        }
        await loadLocalLLMSettings()
    }

    func selectLocalLLMEndpoint(id: UUID) async {
        let configuration = localLLMSettingsStore.load()
        guard configuration.isEnabled, configuration.endpoint(id: id)?.isSelectableAsCurrent == true else {
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return
        }
        let didSave = await localLLMSettingsStore.update(apiKeys: [:]) { configuration in
            guard configuration.isEnabled, configuration.endpoint(id: id)?.isSelectableAsCurrent == true else { return }
            configuration.selectedEndpointID = id
            configuration.selectedProviderID = "openai_compatible:\(id.uuidString.lowercased())"
        }
        guard didSave else {
            localLLMStatusMessage = .message(.nativeSwiftSettingsLocalModelSettingsSaveFailed)
            return
        }
        await loadLocalLLMSettings()
    }

    func loadLocalLLMEndpointAPIKey(id: UUID) async -> String {
        await localLLMSettingsStore.readAPIKey(for: id) ?? ""
    }

    func buildLocalLLMConfiguration(defaultToFirstModel: Bool) -> LocalLLMConfiguration {
        let profile = buildLocalLLMEndpointProfile(id: UUID(), defaultToFirstModel: defaultToFirstModel)
        return LocalLLMConfiguration(isEnabled: true, endpoints: [profile], selectedEndpointID: profile.id)
    }

    private func resetLocalLLMDraft() {
        localLLMDisplayName = ""
        localLLMEndpoint = ""
        localLLMModelsText = ""
        localLLMSelectedModel = ""
        localLLMAPIKey = ""
        localLLMDraftEndpointID = UUID()
    }

    func normalizedModelNames(from text: String) -> [String] {
        text
            .split(whereSeparator: \.isNewline)
            .map { String($0).trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
    }

    private func buildLocalLLMEndpointProfile(id: UUID, defaultToFirstModel: Bool) -> LocalLLMEndpointProfile {
        let models = normalizedModelNames(from: localLLMModelsText)
        let selected = localLLMSelectedModel.trimmingCharacters(in: .whitespacesAndNewlines)
        return LocalLLMEndpointProfile(
            id: id,
            displayName: localLLMDisplayName.trimmingCharacters(in: .whitespacesAndNewlines),
            isEnabled: true,
            endpoint: localLLMEndpoint,
            modelNames: models,
            selectedModelName: selected.isEmpty && defaultToFirstModel ? models.first ?? "" : selected
        )
    }
}

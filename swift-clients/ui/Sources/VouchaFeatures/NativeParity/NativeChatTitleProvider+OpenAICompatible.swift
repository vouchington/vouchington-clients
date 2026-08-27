import Foundation
import VouchaCore
import VouchaLocalization

struct NativeChatLocalTitleProvider: NativeChatTitleProviding {
    private let endpointID: UUID?
    private let settingsStore: LocalLLMSettingsStore
    private let featurePolicy: LocalLLMFeaturePolicy
    private let responsesClient: OpenAICompatibleResponsesClient

    private var configuration: LocalLLMConfiguration {
        settingsStore.load()
    }

    init(
        endpointID: UUID? = nil,
        settingsStore: LocalLLMSettingsStore,
        featurePolicy: LocalLLMFeaturePolicy,
        responsesClient: OpenAICompatibleResponsesClient
    ) {
        self.endpointID = endpointID
        self.settingsStore = settingsStore
        self.featurePolicy = featurePolicy
        self.responsesClient = responsesClient
    }

    var status: NativeChatTitleProviderStatus {
        guard featurePolicy.isEnabled else {
            return .init(isAvailable: false, detail: .message(.nativeSwiftChatLocalModelsUnavailablePlatform))
        }
        guard configuration.isEnabled else {
            return .init(isAvailable: false, detail: .message(.nativeSwiftChatLocalModelSettingsOff))
        }
        guard let endpoint = selectedEndpoint,
              endpoint.responsesURL != nil
        else {
            return .init(isAvailable: false, detail: .message(.nativeSwiftChatEnterLocalResponsesEndpoint))
        }
        guard let model = endpoint.selectedModel else {
            return .init(isAvailable: false, detail: .message(.nativeSwiftChatChooseLocalModel))
        }
        return .init(isAvailable: endpoint.isEnabled, detail: .verbatim(model))
    }

    func generateAssistantResponse(
        to message: String,
        history: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        guard let endpoint = availableEndpoint else { return nil }
        let localHistory = history
            .filter { !$0.isStreaming && !$0.content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .map { LocalLLMChatMessage(role: $0.role.rawValue, content: $0.content) }
        let apiKey = await settingsStore.readAPIKey(for: endpoint.id)
        guard let response = try await responsesClient.generateAssistantResponse(
            message: message,
            history: localHistory,
            endpoint: endpoint,
            apiKey: apiKey
        ) else {
            return nil
        }
        return NativeChatAssistantResponse(
            content: response,
            modelProvider: "openai_compatible",
            modelName: endpoint.selectedModel
        )
    }

    func generateTitle(from messages: [NativeChatTimelineMessage]) async throws -> String? {
        guard let endpoint = availableEndpoint else { return nil }
        let prompt = """
        Suggest a concise conversation title of six words or fewer.
        Return only the title.
        """
        let localHistory = messages
            .suffix(8)
            .map { LocalLLMChatMessage(role: $0.role.rawValue, content: $0.content) }
        let apiKey = await settingsStore.readAPIKey(for: endpoint.id)
        let response = try await responsesClient.generateAssistantResponse(
            message: prompt,
            history: localHistory,
            endpoint: endpoint,
            apiKey: apiKey
        )
        let cleaned = response?
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "\"", with: "")
        return cleaned?.isEmpty == false ? cleaned : nil
    }

    private var selectedEndpoint: LocalLLMEndpointProfile? {
        guard let endpointID else { return configuration.selectedEndpoint }
        return configuration.endpoint(id: endpointID)
    }

    private var availableEndpoint: LocalLLMEndpointProfile? {
        guard featurePolicy.isEnabled,
              configuration.isEnabled,
              let endpoint = selectedEndpoint,
              endpoint.isEnabled,
              endpoint.responsesURL != nil,
              endpoint.selectedModel != nil
        else {
            return nil
        }
        return endpoint
    }
}

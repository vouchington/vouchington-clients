import Foundation
import Observation
import SkipFuse
import VouchaCore

enum AndroidChatProvider: String {
    case aiCore
    case endpoint
}

@Observable @MainActor
final class ViewModel {
    var modelState: AndroidModelState = .checking
    var messages: [AndroidChatMessage] = []
    var draft = ""
    var errorMessage: String?
    var isGenerating = false
    var selectedProvider: AndroidChatProvider = .aiCore

    var endpoint = ""
    var endpointModel = ""
    var endpointAPIKey = "" {
        didSet {
            endpointAPIKeyWasExplicitlyEdited = true
        }
    }

    let runtime: any AndroidAICoreRunning
    let secretStore: any AndroidEndpointSecretStoring
    let settingsStore: LocalLLMSettingsStore
    let endpointClient: any AndroidEndpointGenerating
    var endpointProfileID: UUID
    var endpointAPIKeyWasExplicitlyEdited = false

    init(
        runtime: any AndroidAICoreRunning = AndroidAICoreRuntime(),
        secretStore: any AndroidEndpointSecretStoring = SkipKeychainEndpointSecretStore(),
        settingsStore: LocalLLMSettingsStore = LocalLLMSettingsStore(),
        endpointClient: any AndroidEndpointGenerating = OpenAICompatibleResponsesClient()
    ) {
        self.runtime = runtime
        self.secretStore = secretStore
        self.settingsStore = settingsStore
        self.endpointClient = endpointClient
        let configuration = settingsStore.load()
        let profile = configuration.selectedEndpoint ?? configuration.endpoints.first
        endpointProfileID = profile?.id ?? UUID()
        endpoint = profile?.endpoint ?? ""
        endpointModel = profile?.selectedModelName ?? ""
        endpointAPIKey =
            (try? secretStore.string(forKey: endpointSecretKey(for: endpointProfileID))) ?? ""
        if configuration.selectedProviderID == AndroidChatProvider.endpoint.rawValue,
           profile?.isEnabled == true,
           profile?.responsesURL != nil,
           profile?.selectedModel != nil {
            selectedProvider = .endpoint
        }
    }

    func refreshStatus() async {
        errorMessage = nil
        modelState = .checking
        do {
            modelState = try await runtime.status()
        } catch {
            modelState = .unavailable
            errorMessage = error.localizedDescription
        }
    }

    func downloadModel() async {
        errorMessage = nil
        modelState = .downloading
        do {
            modelState = try await runtime.download()
        } catch is CancellationError {
            modelState = .downloadable
        } catch {
            modelState = .downloadable
            errorMessage = error.localizedDescription
        }
    }

    func sendOnDevice() async {
        let submitted = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !submitted.isEmpty, isSelectedProviderAvailable, !isGenerating else { return }
        draft = ""
        errorMessage = nil
        isGenerating = true
        defer { isGenerating = false }
        let priorMessages = messages
        messages.append(.init(role: "user", content: submitted))

        do {
            let response = try await generateResponse(for: submitted)
            guard !response.isEmpty else { throw AndroidAICoreError.emptyResponse }
            messages.append(.init(role: "assistant", content: response))
        } catch {
            messages = priorMessages
            draft = submitted
            errorMessage = error.localizedDescription
        }
    }

    func boundedPrompt(latest: String) -> String {
        let history = messages.dropLast().suffix(6).map {
            "\($0.role): \(String($0.content.prefix(1_000)))"
        }.joined(separator: "\n")
        let instruction = "You are Voucha's chat assistant. Answer the latest message."
        let latestMessage = "user: \(String(latest.prefix(2_000)))"
        return [instruction, history, latestMessage].joined(separator: "\n")
    }

}

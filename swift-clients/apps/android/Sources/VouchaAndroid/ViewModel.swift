import Foundation
import Observation
import SkipFuse
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

enum AndroidChatProvider: String {
    case aiCore
    case endpoint
}

enum AndroidTranscriptLoadFailure: Equatable {
    case conversations(reset: Bool, after: String?)
    case messages(conversationID: String, reset: Bool, after: String?)
}

struct AndroidFailedGeneration: Equatable {
    let text: String
    let message: String
}

struct AndroidTranscriptLoadFailureRecord: Equatable {
    let operation: AndroidTranscriptLoadFailure
    let message: String
}

struct AndroidFailedTurnAttempt {
    let submitted: String
    let conversationID: String
    let conversationRevision: UUID
    let previousMessages: [AndroidChatMessage]
    let preservingDraft: Bool
}

@Observable @MainActor
final class ViewModel {
    var modelState: AndroidModelState = .checking
    var messages: [AndroidChatMessage] = []
    var draft = ""
    var errorMessage: String? {
        didSet { chatErrorKind = .other }
    }

    var modelErrorMessage: String?
    var chatErrorKind = AndroidChatErrorKind.other
    var conversationCreationFailed = false
    var conversationCreationErrorMessage: String?
    var failedConversationListLoad: AndroidTranscriptLoadFailureRecord?
    var failedConversationHistoryLoad: AndroidTranscriptLoadFailureRecord?
    var latestTranscriptLoadFailure: AndroidTranscriptLoadFailure?
    var loadedConversationHistoryID: String?
    var failedGenerations: [String: AndroidFailedGeneration] = [:]
    var draftBackedGenerationFailures: [String: AndroidFailedGeneration] = [:]
    var generationErrors: [String: String] = [:]
    var pendingTurnErrors: [String: String] = [:]
    var pendingTurnHistoryAcknowledgements: [String: Set<String>] = [:]
    var pendingStoreLoadFailed = false
    var pendingStoreIsLoading = false
    var pendingStoreDiscardFailed = false
    var accountExitIsInProgress = false
    var accountExitErrorKey: UiMessageKey?
    var isGenerating = false
    var selectedProvider: AndroidChatProvider = .aiCore
    var conversations: [ChatConversation] = []
    var selectedConversationID: String?
    var isLoadingConversations = false
    var isLoadingMessages = false
    var isLoadingOlderMessages = false
    var isCreatingConversation = false
    var hasMoreConversations = false
    var hasOlderMessages = false
    var endpoint = ""
    var endpointModel = ""
    var endpointAPIKey = "" {
        didSet {
            endpointAPIKeyWasExplicitlyEdited = true
        }
    }

    let runtime: any AndroidAICoreRunning
    let secretStore: any AndroidEndpointSecretStoring
    let pendingStore: any AndroidPendingTurnStoring
    let settingsStore: LocalLLMSettingsStore
    let endpointClient: any AndroidEndpointGenerating
    var apiClient: APIClient?
    var currentAccountUserID: String?
    var currentPendingTurnOwnerID: String?
    var endpointProfileID: UUID
    var endpointAPIKeyWasExplicitlyEdited = false
    var activeGenerationTask: Task<String, Error>?
    var activeGenerationID = UUID()
    var accountGeneration = UUID()
    var conversationGeneration = UUID()
    var conversationListCursor: String?
    var messagePageCursor: String?
    var pendingTurns: [String: AndroidPendingChatTurn] = [:]
    @ObservationIgnored var lockedPendingTurnsByOwner: [String: [AndroidPendingChatTurn]] = [:]
    @ObservationIgnored var inFlightDiscardsByOwner: [String: AndroidPendingOwnerDiscardInFlight] = [:]
    var activeSendOperationID: UUID?

    init(
        runtime: any AndroidAICoreRunning = AndroidAICoreRuntime(),
        secretStore: any AndroidEndpointSecretStoring = SkipKeychainEndpointSecretStore(),
        pendingStore: any AndroidPendingTurnStoring = AndroidPendingTurnStore(),
        settingsStore: LocalLLMSettingsStore = LocalLLMSettingsStore(),
        endpointClient: any AndroidEndpointGenerating = OpenAICompatibleResponsesClient()
    ) {
        self.runtime = runtime
        self.secretStore = secretStore
        self.pendingStore = AndroidSerializedPendingTurnStore(pendingStore)
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
        modelErrorMessage = nil
        restoreSelectedConversationError()
        modelState = .checking
        defer { restoreSelectedConversationError() }
        do {
            modelState = try await runtime.status()
        } catch {
            modelState = .unavailable
            modelErrorMessage = error.localizedDescription
        }
    }

    func downloadModel() async {
        modelErrorMessage = nil
        restoreSelectedConversationError()
        modelState = .downloading
        defer { restoreSelectedConversationError() }
        do {
            modelState = try await runtime.download()
        } catch is CancellationError {
            modelState = .downloadable
        } catch {
            modelState = .downloadable
            modelErrorMessage = error.localizedDescription
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

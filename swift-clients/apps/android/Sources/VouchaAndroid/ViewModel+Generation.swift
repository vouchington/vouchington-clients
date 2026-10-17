import Foundation
import VouchaCore

extension ViewModel {
    func generateResponse(for message: String) async throws -> String {
        try await generateResponse(for: message, provider: selectedProvider)
    }

    func generateResponse(for message: String, provider: AndroidChatProvider) async throws -> String {
        switch provider {
        case .aiCore:
            let prompt = boundedPrompt(latest: message)
            let runtime = runtime
            return try await performGeneration { try await runtime.generate(prompt: prompt) }
        case .endpoint:
            guard let endpoint = endpointProfile else { throw LocalLLMError.invalidEndpoint }
            let history = messages.dropLast().suffix(6).map {
                LocalLLMChatMessage(role: $0.role, content: String($0.content.prefix(1_000)))
            }
            let apiKey = try secretStore.string(forKey: endpointSecretKey(for: endpoint.id))
            let client = endpointClient
            let submitted = String(message.prefix(2_000))
            return try await performGeneration {
                try await client.generateAssistantResponse(
                    message: submitted, history: history, endpoint: endpoint, apiKey: apiKey
                ) ?? ""
            }
        }
    }

    func performGeneration(_ operation: @escaping @MainActor () async throws -> String) async throws -> String {
        let previous = activeGenerationTask
        let operationID = UUID()
        activeGenerationID = operationID
        if let previous { _ = try? await previous.value }
        guard activeGenerationID == operationID else { throw CancellationError() }
        try Task.checkCancellation()
        let task = Task {
            try Task.checkCancellation()
            return try await operation()
        }
        activeGenerationTask = task
        defer {
            if activeGenerationID == operationID { activeGenerationTask = nil }
        }
        return try await withTaskCancellationHandler {
            try await task.value
        } onCancel: {
            task.cancel()
        }
    }
}

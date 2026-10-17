import Foundation
@testable import VouchaFeatures

final class MutableRetryDraftProvider: NativeChatTitleProviding, @unchecked Sendable {
    var model = "model-a"
    var available = true
    var admittedMCPCall = false
    var titleHold: HeldTitleGeneration?
    var mcpHold: HeldTitleGeneration?
    var mcpFailure: Error?
    private(set) var generationCount = 0
    private(set) var receivedMcpSnapshot = false

    var status: NativeChatTitleProviderStatus {
        .init(isAvailable: available, detail: nil)
    }

    var retryModelIdentity: String? {
        model
    }

    func generateAssistantResponse(
        to _: String,
        history _: [NativeChatTimelineMessage]
    ) async throws -> NativeChatAssistantResponse? {
        generationCount += 1
        return .init(content: "Reply \(generationCount)", modelProvider: "openai_compatible", modelName: model)
    }

    func generateAssistantResponse(
        to message: String, history: [NativeChatTimelineMessage],
        mcpSession _: NativeChatMCPAgentSession?,
        mcpSnapshot: NativeChatMCPAgentSession.ConnectedSnapshot?,
        mcpTurn _: NativeChatMCPTurnContext?
    ) async throws -> NativeChatMCPGenerationOutcome? {
        receivedMcpSnapshot = mcpSnapshot != nil
        if let mcpFailure { throw mcpFailure }
        if let mcpHold {
            await mcpHold.waitUntilReleased()
            throw CancellationError()
        }
        guard let response = try await generateAssistantResponse(to: message, history: history) else { return nil }
        return .answer(.init(
            content: response.content, modelProvider: response.modelProvider, modelName: response.modelName,
            admittedMCPCall: admittedMCPCall
        ))
    }

    func generateTitle(from _: [NativeChatTimelineMessage]) async throws -> String? {
        if let titleHold { await titleHold.waitUntilReleased() }
        return nil
    }
}

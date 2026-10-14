import Foundation
import VouchaCore

#if canImport(FoundationModels)
    import FoundationModels
#endif

extension NativeChatAppleFoundationModelsProvider {
    func generateAssistantResponse(
        to message: String,
        history: [NativeChatTimelineMessage],
        mcpSession: NativeChatMCPAgentSession?,
        mcpSnapshot: NativeChatMCPAgentSession.ConnectedSnapshot?
    ) async throws -> NativeChatAssistantResponse? {
        guard let mcpSession, let snapshot = mcpSnapshot else {
            return try await generateAssistantResponse(to: message, history: history)
        }
        #if canImport(FoundationModels)
            guard #available(macOS 26.4, iOS 26.4, *) else {
                return try await generateAssistantResponse(to: message, history: history)
            }
            guard status.isAvailable else { return nil }
            let bridge = try NativeChatMCPToolBridge(catalog: snapshot.catalog)
            let projected = NativeChatFoundationModelsToolSelection.select(bridge, matching: message)
            guard !projected.isEmpty else {
                return try await generateAssistantResponse(to: message, history: history)
            }
            let state = await NativeChatFoundationModelsToolState()
            let tools: [any Tool] = projected.map { item in
                NativeChatFoundationModelsTool(
                    name: item.descriptor.name, description: item.descriptor.description,
                    parameters: item.schema,
                    bridge: bridge, session: mcpSession, snapshot: snapshot, state: state
                )
            }
            let modelSession = LanguageModelSession(model: SystemLanguageModel.default, tools: tools)
            let response: LanguageModelSession.Response<String>
            do {
                response = try await modelSession.respond(to: Self.assistantPrompt(
                    message: message, history: history,
                    maximumCharacters: NativeChatFoundationModelsToolSelection.maximumPromptCharacters
                ))
            } catch {
                if await state.attempted { throw LocalLLMToolLoopFailure.partialExecution }
                throw error
            }
            guard await mcpSession.isCurrent(snapshot) else {
                if await state.attempted { throw LocalLLMToolLoopFailure.partialExecution }
                throw CancellationError()
            }
            let content = response.content.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !content.isEmpty else {
                if await state.attempted { throw LocalLLMToolLoopFailure.partialExecution }
                return nil
            }
            return NativeChatAssistantResponse(
                content: content, modelProvider: "apple_foundation", modelName: "apple-foundation-system"
            )
        #else
            return nil
        #endif
    }
}

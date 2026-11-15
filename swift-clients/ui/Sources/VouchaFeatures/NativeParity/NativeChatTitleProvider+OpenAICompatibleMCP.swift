import Foundation
import VouchaCore

extension NativeChatLocalTitleProvider {
    /// Runs the HTTP tool loop. Facts and the complete validated envelope are published
    /// per call under the trusted account, revision, turn and provider call identity.
    func generateWithMCPTools(
        request: LocalLLMToolRequest, bridge: NativeChatMCPToolBridge,
        connection: (session: NativeChatMCPAgentSession, snapshot: NativeChatMCPAgentSession.ConnectedSnapshot),
        turn: NativeChatMCPTurnContext?
    ) async throws -> NativeChatMCPGenerationOutcome? {
        let (session, snapshot) = connection
        guard let turn, turn.owner.accountId == snapshot.accountId,
              turn.owner.revision == snapshot.revision else { throw LocalLLMToolLoopFailure.toolNotAdmitted }
        let state = await NativeChatMCPHTTPToolState(turn: turn)
        let content: String?
        do {
            content = try await responsesClient.generateAssistantResponseWithTools(
                request: request,
                isCurrent: {
                    guard await session.isCurrent(snapshot) else { return false }
                    return await turn.isCurrent()
                },
                validate: { name, arguments in try bridge.validate(name: name, rawArguments: arguments) },
                executeCall: { call in
                    let result = try await bridge.executeResult(
                        name: call.name, rawArguments: call.arguments, session: session,
                        accountId: snapshot.accountId, revision: snapshot.revision,
                        onStarted: { state.markStarted() }
                    )
                    try await state.record(callId: call.callID, name: call.name, result: result)
                    return result.envelope
                }
            )
        } catch LocalLLMToolLoopFailure.partialExecution {
            return try await retainedOutcome(state, session: session, snapshot: snapshot)
        }
        let attempted = await state.attempted
        if attempted {
            guard await session.isCurrent(snapshot) else { throw LocalLLMToolLoopFailure.partialExecution }
        }
        guard let content else { return nil }
        return .answer(NativeChatAssistantResponse(
            content: content, modelProvider: "openai_compatible", modelName: request.endpoint.selectedModel,
            admittedMCPCall: attempted
        ))
    }

    /// Model continuation failed. Publish completed-action status only when no call is ambiguous.
    private func retainedOutcome(
        _ state: NativeChatMCPHTTPToolState, session: NativeChatMCPAgentSession,
        snapshot: NativeChatMCPAgentSession.ConnectedSnapshot
    ) async throws -> NativeChatMCPGenerationOutcome {
        guard await state.completedAllCalls else { throw LocalLLMToolLoopFailure.partialExecution }
        if Task.isCancelled { throw CancellationError() }
        guard await session.isCurrent(snapshot) else { throw LocalLLMToolLoopFailure.partialExecution }
        return .toolResultsOnly(.processingStopped)
    }
}

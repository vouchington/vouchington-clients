import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization

extension NativeChatViewModel {
    func generateLocalDraftMessage(
        client: APIClient,
        provider: any NativeChatTitleProviding,
        context: NativeChatDraftSendContext
    ) async {
        guard isCurrentLocalGeneration(context) else {
            discardStaleLocalTurn(userMessageId: context.userMessageId, assistantMessageId: nil)
            return
        }

        let mcpSnapshot: NativeChatMCPAgentSession.ConnectedSnapshot? = switch context.providerSelection {
        case .openAICompatible, .appleFoundationModels:
            mcpSession?.connectedSnapshot().flatMap { $0.accountId == currentMemberId ? $0 : nil }
        default: nil
        }
        let assistantMessageId = context.assistantMessageId ?? NativeChatMessageIDs.nextLocalTurn().assistant
        pendingLocalAssistantMessageId = assistantMessageId
        ensureAssistantMessage(id: assistantMessageId)

        do {
            try await performLocalGeneration(
                client: client, provider: provider, context: context,
                assistantMessageId: assistantMessageId, mcpSnapshot: mcpSnapshot
            )
        } catch LocalLLMToolLoopFailure.partialExecution {
            failLocalGeneration(
                context: context,
                assistantMessageId: assistantMessageId,
                message: .message(.sharedChatMcpPossibleSideEffect),
                restoreDraft: false
            )
        } catch let error as VouchaError {
            failLocalGeneration(
                context: context,
                assistantMessageId: assistantMessageId,
                message: error.errorDescription.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftChatUnableToPersistOnDeviceResponse)
            )
        } catch {
            if let mcpSnapshot, !isCurrentMCPSnapshot(mcpSnapshot) {
                abortInvalidatedMCPTurn(context: context, snapshot: mcpSnapshot)
                return
            }
            failLocalGeneration(
                context: context,
                assistantMessageId: assistantMessageId,
                message: .verbatim(error.localizedDescription)
            )
        }
    }

    private func performLocalGeneration(
        client: APIClient, provider: any NativeChatTitleProviding,
        context: NativeChatDraftSendContext,
        assistantMessageId: String, mcpSnapshot: NativeChatMCPAgentSession.ConnectedSnapshot?
    ) async throws {
        if try await persistPendingLocalTurnIfAny(
            client: client, context: context, assistantMessageId: assistantMessageId
        ) { return }
        guard let outcome = try await provider.generateAssistantResponse(
            to: context.text,
            history: localGenerationHistory(excluding: context.userMessageId),
            mcpSession: mcpSession,
            mcpSnapshot: mcpSnapshot,
            mcpTurn: prepareMCPReceiptTurn(context: context, snapshot: mcpSnapshot)
        ) else {
            failLocalGeneration(
                context: context,
                assistantMessageId: assistantMessageId,
                message: provider.status.detail ?? .message(.nativeSwiftChatOnDeviceUnavailable)
            )
            return
        }
        invalidateMCPReceiptsIfNeeded()
        guard isCurrentLocalGeneration(context) else {
            discardStaleLocalTurn(
                userMessageId: context.userMessageId,
                assistantMessageId: assistantMessageId
            )
            return
        }
        if let mcpSnapshot, !matchesMCPAccount(mcpSnapshot) {
            abortInvalidatedMCPTurn(context: context, snapshot: mcpSnapshot)
            return
        }

        guard case let .answer(response) = outcome else {
            if case let .toolResultsOnly(continuation) = outcome {
                finishLocalToolResults(
                    context: context,
                    assistantMessageId: assistantMessageId,
                    continuation: continuation
                )
            }
            return
        }
        try await persistLocalGenerationResponse(
            client: client,
            context: context,
            assistantMessageId: assistantMessageId,
            response: response,
            mcpSnapshot: response.persistenceSnapshot(mcpSnapshot, providerSelection: context.providerSelection)
        )
    }

    func isCurrentLocalGeneration(_ context: NativeChatDraftSendContext) -> Bool {
        streamingConversationId == context.conversationId
            && streamingUserMessageId == context.userMessageId
    }

    func isCurrentMCPSnapshot(_ snapshot: NativeChatMCPAgentSession.ConnectedSnapshot) -> Bool {
        currentMemberId == snapshot.accountId && mcpSession?.isCurrent(snapshot) == true
    }

    func matchesMCPAccount(_ snapshot: NativeChatMCPAgentSession.ConnectedSnapshot) -> Bool {
        currentMemberId == snapshot.accountId && mcpSession?.accountId == snapshot.accountId
    }

    func abortInvalidatedMCPTurn(
        context: NativeChatDraftSendContext,
        snapshot: NativeChatMCPAgentSession.ConnectedSnapshot
    ) {
        invalidateMCPReceiptsIfNeeded()
        if isCurrentLocalGeneration(context),
           currentMemberId == snapshot.accountId,
           !draftEditedDuringSend {
            draftMessage = context.text
        }
        abortStreaming()
    }

    func discardStaleLocalTurn(userMessageId: String, assistantMessageId: String?) {
        if streamingUserMessageId == userMessageId {
            streamingUserMessageId = nil
        }
        if streamingAssistantMessageId == assistantMessageId {
            streamingAssistantMessageId = nil
        }
        if pendingLocalAssistantMessageId == assistantMessageId {
            pendingLocalAssistantMessageId = nil
        }
        if !retainTemporaryMCPUserTurn(userMessageId) { discardConversationMessage(id: userMessageId) }
        if let assistantMessageId { discardConversationMessage(id: assistantMessageId) }
    }

    func localGenerationHistory(excluding userMessageId: String) -> [NativeChatTimelineMessage] {
        messages.filter {
            $0.id != userMessageId && !($0.role == .assistant && $0.isStreaming)
        }
    }

}

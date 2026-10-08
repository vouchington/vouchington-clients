import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

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

        let history = localGenerationHistory(excluding: context.userMessageId)
        let assistantMessageId = context.assistantMessageId ?? NativeChatMessageIDs.nextLocalTurn().assistant
        pendingLocalAssistantMessageId = assistantMessageId
        ensureAssistantMessage(id: assistantMessageId)

        do {
            if try await persistPendingLocalTurnIfAny(
                client: client, context: context, assistantMessageId: assistantMessageId
            ) { return }
            guard let response = try await provider.generateAssistantResponse(
                to: context.text,
                history: history
            ) else {
                failLocalGeneration(
                    context: context,
                    assistantMessageId: assistantMessageId,
                    message: provider.status.detail ?? .message(.nativeSwiftChatOnDeviceUnavailable)
                )
                return
            }
            guard isCurrentLocalGeneration(context) else {
                discardStaleLocalTurn(
                    userMessageId: context.userMessageId,
                    assistantMessageId: assistantMessageId
                )
                return
            }

            try await persistLocalGenerationResponse(
                client: client,
                context: context,
                assistantMessageId: assistantMessageId,
                response: response
            )
        } catch let error as VouchaError {
            failLocalGeneration(
                context: context,
                assistantMessageId: assistantMessageId,
                message: error.errorDescription.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftChatUnableToPersistOnDeviceResponse)
            )
        } catch {
            failLocalGeneration(
                context: context,
                assistantMessageId: assistantMessageId,
                message: .verbatim(error.localizedDescription)
            )
        }
    }

    func persistLocalGenerationResponse(
        client: APIClient,
        context: NativeChatDraftSendContext,
        assistantMessageId: String,
        response: NativeChatAssistantResponse
    ) async throws {
        pendingLocalTurn = .init(context: context, response: response)
        let persistedResponse: ClientGeneratedChatResponse = try await client.send(.clientGeneratedChat(
            conversationId: context.conversationId,
            message: context.text,
            messageIds: (user: context.userMessageId, assistant: assistantMessageId),
            assistantContent: response.content,
            model: (provider: response.modelProvider, name: response.modelName)
        ))
        guard isCurrentLocalGeneration(context) else {
            discardStaleLocalTurn(
                userMessageId: context.userMessageId,
                assistantMessageId: assistantMessageId
            )
            return
        }
        if pendingLocalTurn?.context.userMessageId == context.userMessageId {
            pendingLocalTurn = nil
        }
        reconcileClientGeneratedMessages(
            userMessageId: context.userMessageId,
            assistantMessageId: assistantMessageId,
            response: persistedResponse
        )
        await finishStream(
            conversationId: context.conversationId,
            userMessageId: persistedResponse.userMessage.id,
            createdConversation: context.createdConversation
        )
    }

    func isCurrentLocalGeneration(_ context: NativeChatDraftSendContext) -> Bool {
        streamingConversationId == context.conversationId
            && streamingUserMessageId == context.userMessageId
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
        discardConversationMessage(id: userMessageId)
        if let assistantMessageId { discardConversationMessage(id: assistantMessageId) }
    }

    func localGenerationHistory(excluding userMessageId: String) -> [NativeChatTimelineMessage] {
        messages.filter {
            $0.id != userMessageId && !($0.role == .assistant && $0.isStreaming)
        }
    }

    private func failLocalGeneration(
        context: NativeChatDraftSendContext,
        assistantMessageId: String,
        message: UiVerbatimText
    ) {
        guard isCurrentLocalGeneration(context) else {
            discardStaleLocalTurn(
                userMessageId: context.userMessageId,
                assistantMessageId: assistantMessageId
            )
            return
        }
        failUnpersistedLocalGeneration(
            context: context,
            assistantMessageId: assistantMessageId,
            message: message
        )
    }

    func failUnpersistedLocalGeneration(
        context: NativeChatDraftSendContext,
        assistantMessageId: String?,
        message: UiVerbatimText
    ) {
        guard isCurrentLocalGeneration(context) else {
            discardConversationMessage(id: context.userMessageId)
            if let assistantMessageId { discardConversationMessage(id: assistantMessageId) }
            return
        }
        discardConversationMessage(id: context.userMessageId)
        if let assistantMessageId {
            discardConversationMessage(id: assistantMessageId)
        }
        draftMessage = context.text
        if streamingUserMessageId == context.userMessageId {
            streamingUserMessageId = nil
        }
        if streamingAssistantMessageId == assistantMessageId {
            streamingAssistantMessageId = nil
        }
        if pendingLocalAssistantMessageId == assistantMessageId {
            pendingLocalAssistantMessageId = nil
        }
        streamErrorMessage = message
        isStreaming = false
        streamingConversationId = nil
        streamedContent = ""
        streamTask = nil
    }

    private func reconcileClientGeneratedMessages(
        userMessageId: String,
        assistantMessageId: String,
        response: ClientGeneratedChatResponse
    ) {
        updateConversationMessage(id: userMessageId) { message in
            message.id = response.userMessage.id
            message.content = response.userMessage.content.displayText
            message.error = response.userMessage.content.error.map(UiVerbatimText.verbatim)
        }
        updateConversationMessage(id: assistantMessageId) { message in
            message.id = response.assistantMessage.id
            message.content = response.assistantMessage.content.displayText
            message.error = response.assistantMessage.content.error.map(UiVerbatimText.verbatim)
            message.isStreaming = false
        }
        streamingUserMessageId = response.userMessage.id
        streamingAssistantMessageId = response.assistantMessage.id
        pendingLocalAssistantMessageId = nil
        streamedContent = response.assistantMessage.content.displayText
    }
}

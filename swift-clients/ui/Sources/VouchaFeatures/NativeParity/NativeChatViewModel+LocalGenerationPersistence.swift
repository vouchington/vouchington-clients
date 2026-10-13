import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeChatViewModel {
    func persistLocalGenerationResponse(
        client: APIClient,
        context: NativeChatDraftSendContext,
        assistantMessageId: String,
        response: NativeChatAssistantResponse,
        mcpSnapshot: NativeChatMCPAgentSession.ConnectedSnapshot? = nil
    ) async throws {
        pendingLocalTurn = .init(context: context, response: response)
        let persistedResponse: ClientGeneratedChatResponse = try await client.send(.clientGeneratedChat(
            conversationId: context.conversationId,
            message: context.text,
            messageIds: (user: context.userMessageId, assistant: assistantMessageId),
            assistantContent: response.content,
            model: (provider: response.modelProvider, name: response.modelName)
        ))
        if let mcpSnapshot, mcpSession?.isCurrent(mcpSnapshot) != true {
            abortStreaming()
            return
        }
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

    func reconcileClientGeneratedMessages(
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

import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension ViewModel {
    func sendOnDevice() async {
        let submitted = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard canSend(submitted), let client = apiClient, let userID = currentAccountUserID else { return }

        let accountRevision = accountGeneration
        let operationID = UUID()
        activeSendOperationID = operationID
        isGenerating = true
        transcriptLoadFailed = false
        defer { finishSendOperation(operationID, accountRevision: accountRevision) }

        if selectedConversationID == nil {
            await createConversation()
            guard accountGeneration == accountRevision,
                  currentAccountUserID == userID,
                  selectedConversationID != nil
            else { return }
        }
        guard let conversationID = selectedConversationID else { return }
        await generateAndPersistTurn(
            submitted,
            conversationID: conversationID,
            client: client,
            userID: userID,
            accountRevision: accountRevision
        )
    }

    private func canSend(_ submitted: String) -> Bool {
        !submitted.isEmpty && isSelectedProviderAvailable && !isGenerating && !isLoadingMessages &&
            !isLoadingConversations && pendingTurnForSelectedConversation == nil
    }

    private func generateAndPersistTurn(
        _ submitted: String,
        conversationID: String,
        client: APIClient,
        userID: String,
        accountRevision: UUID
    ) async {
        let conversationRevision = conversationGeneration
        let previousMessages = messages
        let ids = AndroidPendingChatTurn.nextMessageIDs()
        let selectedProvider = selectedProvider
        let model = modelIdentity(for: selectedProvider)
        draft = ""
        errorMessage = nil
        messages.append(.init(id: ids.user, role: "user", content: submitted))

        do {
            let response = try await generateResponse(for: submitted, provider: selectedProvider)
            guard accountGeneration == accountRevision, currentAccountUserID == userID else { return }
            guard !response.isEmpty else { throw AndroidAICoreError.emptyResponse }
            let pending = AndroidPendingChatTurn(
                conversationID: conversationID,
                userMessageID: ids.user,
                assistantMessageID: ids.assistant,
                userText: submitted,
                assistantText: response,
                modelProvider: model.provider,
                modelName: model.name
            )
            pendingTurns[conversationID] = pending
            if selectedConversationID == conversationID, conversationGeneration == conversationRevision {
                messages.append(.init(id: ids.assistant, role: "assistant", content: response))
            }
            try await persist(pending, client: client, accountRevision: accountRevision, userID: userID)
        } catch {
            guard accountGeneration == accountRevision, currentAccountUserID == userID else { return }
            recoverFailedTurn(
                submitted,
                conversationID: conversationID,
                conversationRevision: conversationRevision,
                previousMessages: previousMessages,
                error: error
            )
        }
    }

    private func recoverFailedTurn(
        _ submitted: String,
        conversationID: String,
        conversationRevision: UUID,
        previousMessages: [AndroidChatMessage],
        error: Error
    ) {
        if pendingTurns[conversationID] != nil {
            if selectedConversationID == conversationID {
                appendPendingTurnIfNeeded(for: conversationID)
                errorMessage = error.localizedDescription
            }
            return
        }
        guard selectedConversationID == conversationID,
              conversationGeneration == conversationRevision
        else { return }
        messages = previousMessages
        draft = submitted
        errorMessage = error.localizedDescription
    }

    func retryPendingTurn() async {
        guard let pending = pendingTurnForSelectedConversation,
              let client = apiClient,
              let userID = currentAccountUserID,
              !isGenerating,
              !isLoadingMessages
        else { return }
        let accountRevision = accountGeneration
        let operationID = UUID()
        activeSendOperationID = operationID
        isGenerating = true
        errorMessage = nil
        transcriptLoadFailed = false
        defer { finishSendOperation(operationID, accountRevision: accountRevision) }
        do {
            try await persist(pending, client: client, accountRevision: accountRevision, userID: userID)
        } catch {
            guard accountGeneration == accountRevision, currentAccountUserID == userID else { return }
            errorMessage = error.localizedDescription
        }
    }

    private func persist(
        _ pending: AndroidPendingChatTurn,
        client: APIClient,
        accountRevision: UUID,
        userID: String
    ) async throws {
        let response: ClientGeneratedChatResponse = try await client.send(.clientGeneratedChat(
            conversationId: pending.conversationID,
            message: pending.userText,
            messageIds: (user: pending.userMessageID, assistant: pending.assistantMessageID),
            assistantContent: pending.assistantText,
            model: (provider: pending.modelProvider, name: pending.modelName)
        ))
        guard accountGeneration == accountRevision, currentAccountUserID == userID else { return }
        guard response.turn.userMessageId == pending.userMessageID,
              response.turn.assistantMessageId == pending.assistantMessageID,
              response.userMessage.id == pending.userMessageID,
              response.assistantMessage.id == pending.assistantMessageID,
              response.userMessage.conversationId == pending.conversationID,
              response.assistantMessage.conversationId == pending.conversationID,
              response.userMessage.content.role == "user",
              response.assistantMessage.content.role == "assistant"
        else {
            throw AndroidTranscriptSendError.responseMismatch
        }
        guard pendingTurns[pending.conversationID] == pending else { return }
        pendingTurns.removeValue(forKey: pending.conversationID)
        guard selectedConversationID == pending.conversationID else { return }
        messages.removeAll {
            $0.id == pending.userMessageID || $0.id == pending.assistantMessageID ||
                $0.id == response.userMessage.id || $0.id == response.assistantMessage.id
        }
        messages.append(.init(
            id: response.userMessage.id,
            role: "user",
            content: response.userMessage.content.displayText,
            createdAt: response.userMessage.createdAt
        ))
        messages.append(.init(
            id: response.assistantMessage.id,
            role: "assistant",
            content: response.assistantMessage.content.displayText,
            createdAt: response.assistantMessage.createdAt
        ))
        sortMessagesChronologically()
    }

    private func modelIdentity(for provider: AndroidChatProvider) -> (provider: String, name: String?) {
        switch provider {
        case .aiCore:
            ("android_aicore", "android-aicore-system")
        case .endpoint:
            ("openai_compatible", endpointProfile?.selectedModel)
        }
    }

    private func finishSendOperation(_ operationID: UUID, accountRevision: UUID) {
        guard accountGeneration == accountRevision, activeSendOperationID == operationID else { return }
        activeSendOperationID = nil
        isGenerating = false
    }
}

private enum AndroidTranscriptSendError: Error {
    case responseMismatch
}

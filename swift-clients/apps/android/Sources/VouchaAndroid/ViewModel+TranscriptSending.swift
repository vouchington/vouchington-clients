import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension ViewModel {
    func sendOnDevice() async {
        let draftAtSubmission = draft
        let submitted = draftAtSubmission.trimmingCharacters(in: .whitespacesAndNewlines)
        guard canSend(submitted), let client = apiClient, let userID = currentAccountUserID else { return }

        let accountRevision = accountGeneration
        let operationID = UUID()
        activeSendOperationID = operationID
        isGenerating = true
        defer { finishSendOperation(operationID, accountRevision: accountRevision) }

        if selectedConversationID == nil {
            guard await createConversation(), accountGeneration == accountRevision,
                  currentAccountUserID == userID,
                  let conversationID = selectedConversationID,
                  loadedConversationHistoryID == conversationID
            else { return }
        }
        guard let conversationID = selectedConversationID else { return }
        await generateAndPersistTurn(
            submitted,
            conversationID: conversationID,
            client: client,
            userID: userID,
            accountRevision: accountRevision,
            draftAtSubmission: draftAtSubmission
        )
    }

    private func canSend(_ submitted: String) -> Bool {
        !submitted.isEmpty && canSendTranscriptMessage
    }

    func generateAndPersistTurn(
        _ submitted: String,
        conversationID: String,
        client: APIClient,
        userID: String,
        accountRevision: UUID,
        preservingDraft: Bool = false,
        draftAtSubmission: String? = nil
    ) async {
        let conversationRevision = conversationGeneration
        let previousMessages = messages
        let ids = AndroidPendingChatTurn.nextMessageIDs()
        let selectedProvider = selectedProvider
        let model = modelIdentity(for: selectedProvider)
        clearSubmittedDraftIfUnchanged(draftAtSubmission, preservingDraft: preservingDraft)
        clearErrorsForNewGeneration(in: conversationID)
        messages.append(.init(id: ids.user, role: "user", content: submitted))
        var completedPending: AndroidPendingChatTurn?

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
            completedPending = pending
            failedGenerations.removeValue(forKey: conversationID)
            pendingTurns[conversationID] = pending
            pendingTurnErrors.removeValue(forKey: conversationID)
            pendingTurnHistoryAcknowledgements.removeValue(forKey: conversationID)
            if selectedConversationID == conversationID, conversationGeneration == conversationRevision {
                messages.append(.init(id: ids.assistant, role: "assistant", content: response))
            }
            try await persist(pending, client: client, accountRevision: accountRevision, userID: userID)
        } catch {
            guard accountGeneration == accountRevision, currentAccountUserID == userID else { return }
            if let completedPending, pendingTurns[conversationID] != completedPending {
                return
            }
            recoverFailedTurn(
                AndroidFailedTurnAttempt(
                    submitted: submitted,
                    conversationID: conversationID,
                    conversationRevision: conversationRevision,
                    previousMessages: previousMessages,
                    preservingDraft: preservingDraft
                ),
                error: error
            )
        }
    }

    func retryPendingTurn() async {
        guard let pending = pendingTurnForSelectedConversation,
              let client = apiClient,
              let userID = currentAccountUserID,
              !isGenerating,
              !isLoadingMessages,
              !isLoadingConversations,
              !pendingStoreIsLoading,
              !accountExitIsInProgress,
              !isCreatingConversation
        else { return }
        let accountRevision = accountGeneration
        let operationID = UUID()
        activeSendOperationID = operationID
        isGenerating = true
        pendingTurnErrors.removeValue(forKey: pending.conversationID)
        errorMessage = nil
        conversationCreationFailed = false
        conversationCreationErrorMessage = nil
        defer { finishSendOperation(operationID, accountRevision: accountRevision) }
        do {
            try await persist(pending, client: client, accountRevision: accountRevision, userID: userID)
            restoreErrorAfterPendingRetry(
                accountRevision: accountRevision,
                userID: userID,
                conversationID: pending.conversationID
            )
        } catch {
            guard accountGeneration == accountRevision,
                  currentAccountUserID == userID,
                  pendingTurns[pending.conversationID] == pending
            else { return }
            let message = error.localizedDescription
            pendingTurnErrors[pending.conversationID] = message
            if selectedConversationID == pending.conversationID {
                errorMessage = message
            }
        }
    }

    private func persist(
        _ pending: AndroidPendingChatTurn,
        client: APIClient,
        accountRevision: UUID,
        userID: String
    ) async throws {
        guard let ownerID = AndroidPendingTurnOwnerNamespace.key(userID: userID, baseURL: client.baseURL),
              currentPendingTurnOwnerID == ownerID
        else { throw AndroidPendingTurnStoreError.invalidBridgeResponse }
        try await pendingStore.save(ownerID: ownerID, turn: pending)
        forgetLockedPendingTurn(pending, ownerID: ownerID)
        guard accountGeneration == accountRevision, currentAccountUserID == userID,
              currentPendingTurnOwnerID == ownerID, pendingTurns[pending.conversationID] == pending else { return }
        let response: ClientGeneratedChatResponse = try await client.send(.clientGeneratedChat(
            conversationId: pending.conversationID,
            serializedBody: pending.requestBody
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
        try await pendingStore.delete(ownerID: ownerID, turn: pending)
        guard accountGeneration == accountRevision, currentAccountUserID == userID,
              currentPendingTurnOwnerID == ownerID, pendingTurns[pending.conversationID] == pending else { return }
        pendingTurns.removeValue(forKey: pending.conversationID)
        pendingTurnErrors.removeValue(forKey: pending.conversationID)
        pendingTurnHistoryAcknowledgements.removeValue(forKey: pending.conversationID)
        guard selectedConversationID == pending.conversationID else { return }
        messages.removeAll {
            $0.id == pending.userMessageID || $0.id == pending.assistantMessageID ||
                $0.id == response.userMessage.id || $0.id == response.assistantMessage.id
        }
        messages.append(.init(transcriptMessage: response.userMessage))
        messages.append(.init(transcriptMessage: response.assistantMessage))
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
}

private enum AndroidTranscriptSendError: Error {
    case responseMismatch
}

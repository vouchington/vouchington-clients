import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension ViewModel {
    func retryConversationLoad() async {
        switch latestTranscriptLoadFailure {
        case let .conversations(reset, cursor):
            await loadConversationPage(reset: reset, retryCursor: cursor, useRetryCursor: true)
            if reset, selectedConversationID == nil, let first = conversations.first {
                await selectConversation(first.id)
            }
        case let .messages(conversationID, reset, cursor) where conversationID == selectedConversationID:
            await loadConversationMessages(
                reset: reset,
                retryCursor: cursor,
                useRetryCursor: true
            )
        case .some(.messages), .none:
            if let failedConversationListLoad {
                if case let .conversations(reset, cursor) = failedConversationListLoad.operation {
                    await loadConversationPage(reset: reset, retryCursor: cursor, useRetryCursor: true)
                    if reset, selectedConversationID == nil, let first = conversations.first {
                        await selectConversation(first.id)
                    }
                }
            } else if let failedConversationHistoryLoad,
                      case let .messages(conversationID, reset, cursor) = failedConversationHistoryLoad.operation,
                      conversationID == selectedConversationID {
                await loadConversationMessages(
                    reset: reset,
                    retryCursor: cursor,
                    useRetryCursor: true
                )
            }
        }
    }

    func retryFailedGeneration() async {
        guard let failed = failedGenerationForSelectedConversation,
              canRetryFailedGeneration,
              let conversationID = selectedConversationID,
              let client = apiClient,
              let userID = currentAccountUserID
        else { return }
        let accountRevision = accountGeneration
        let operationID = UUID()
        activeSendOperationID = operationID
        isGenerating = true
        defer { finishSendOperation(operationID, accountRevision: accountRevision) }
        await generateAndPersistTurn(
            failed.text,
            conversationID: conversationID,
            client: client,
            userID: userID,
            accountRevision: accountRevision,
            preservingDraft: true
        )
    }

    private var canRetryFailedGeneration: Bool {
        isSelectedProviderAvailable && !isGenerating && !isCreatingConversation &&
            !isLoadingMessages && !isLoadingConversations && pendingTurnForSelectedConversation == nil &&
            (selectedConversationID == nil || loadedConversationHistoryID == selectedConversationID)
    }

    func restoreSelectedConversationError() {
        let pendingError = selectedConversationID.flatMap { pendingTurnErrors[$0] }
        errorMessage = transcriptLoadErrorMessage ?? pendingError ?? failedGenerationForSelectedConversation?.message
    }

    func recoverFailedTurn(_ attempt: AndroidFailedTurnAttempt, error: Error) {
        let conversationID = attempt.conversationID
        if pendingTurns[conversationID] != nil {
            let message = error.localizedDescription
            pendingTurnErrors[conversationID] = message
            if selectedConversationID == conversationID {
                appendPendingTurnIfNeeded(for: conversationID)
                errorMessage = message
            }
            return
        }
        guard selectedConversationID == conversationID,
              conversationGeneration == attempt.conversationRevision
        else {
            retainFailedGeneration(attempt.submitted, for: conversationID, error: error)
            return
        }
        messages = attempt.previousMessages
        if attempt.preservingDraft || !draft.isEmpty {
            retainFailedGeneration(attempt.submitted, for: conversationID, error: error)
        } else {
            draft = attempt.submitted
        }
        errorMessage = error.localizedDescription
        chatErrorKind = .generation
    }

    private func retainFailedGeneration(_ text: String, for conversationID: String, error: Error) {
        failedGenerations[conversationID] = AndroidFailedGeneration(
            text: text,
            message: error.localizedDescription
        )
    }

    func clearLoadFailureIfMatching(_ operation: AndroidTranscriptLoadFailure) {
        switch operation {
        case .conversations:
            if failedConversationListLoad?.operation == operation { failedConversationListLoad = nil }
        case let .messages(conversationID, _, _):
            if failedConversationHistoryLoad?.operation == operation,
               conversationID == selectedConversationID {
                failedConversationHistoryLoad = nil
            }
        }
        if latestTranscriptLoadFailure == operation {
            latestTranscriptLoadFailure = failedConversationHistoryLoad?.operation ?? failedConversationListLoad?
                .operation
        }
        restoreSelectedConversationError()
    }
}

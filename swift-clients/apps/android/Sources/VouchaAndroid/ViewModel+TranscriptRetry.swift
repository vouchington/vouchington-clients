import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension ViewModel {
    func clearErrorsForNewGeneration(in conversationID: String) {
        generationErrors.removeValue(forKey: conversationID)
        draftBackedGenerationFailures.removeValue(forKey: conversationID)
        conversationCreationFailed = false
        conversationCreationErrorMessage = nil
        errorMessage = nil
    }

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
        let generationError = generationErrorForSelectedConversation
        let creationError = conversationCreationFailed ? conversationCreationErrorMessage : nil
        let restoreGenerationKind = pendingError == nil && generationError != nil
        let transcriptError = pendingError ?? generationError ?? creationError ?? transcriptLoadErrorMessage
        let selectedError = transcriptError ?? modelErrorMessage
        errorMessage = selectedError
        if restoreGenerationKind {
            chatErrorKind = .generation
        } else if transcriptError == nil, modelErrorMessage != nil {
            chatErrorKind = .model
        }
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
        generationErrors[conversationID] = error.localizedDescription
        guard selectedConversationID == conversationID,
              conversationGeneration == attempt.conversationRevision
        else {
            retainFailedGeneration(attempt.submitted, for: conversationID, error: error)
            if selectedConversationID == conversationID {
                errorMessage = error.localizedDescription
                chatErrorKind = .generation
            }
            return
        }
        messages = attempt.previousMessages
        if attempt.preservingDraft || !draft.isEmpty {
            retainFailedGeneration(attempt.submitted, for: conversationID, error: error)
        } else {
            draft = attempt.submitted
            draftBackedGenerationFailures[conversationID] = AndroidFailedGeneration(
                text: attempt.submitted,
                message: error.localizedDescription
            )
        }
        errorMessage = error.localizedDescription
        chatErrorKind = .generation
    }

    private func retainFailedGeneration(_ text: String, for conversationID: String, error: Error) {
        draftBackedGenerationFailures.removeValue(forKey: conversationID)
        failedGenerations[conversationID] = AndroidFailedGeneration(
            text: text,
            message: error.localizedDescription
        )
    }

    func clearSubmittedDraftIfUnchanged(_ submittedDraft: String?, preservingDraft: Bool) {
        guard !preservingDraft, let submittedDraft, draft == submittedDraft else { return }
        draft = ""
    }

    func restoreErrorAfterPendingRetry(accountRevision: UUID, userID: String, conversationID: String) {
        guard accountGeneration == accountRevision, currentAccountUserID == userID,
              selectedConversationID == conversationID, errorMessage == nil
        else { return }
        restoreSelectedConversationError()
    }

    func clearLoadFailureIfMatching(_ operation: AndroidTranscriptLoadFailure) {
        switch operation {
        case .conversations:
            if failedConversationListLoad?.operation == operation {
                failedConversationListLoad = nil
            }
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

import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension ViewModel {
    func loadConversationPage(
        reset: Bool,
        retryCursor: String? = nil,
        useRetryCursor: Bool = false
    ) async {
        guard let client = apiClient, currentAccountUserID != nil,
              !accountExitIsInProgress,
              !isLoadingConversations, !isCreatingConversation, reset || hasMoreConversations
        else { return }
        let accountRevision = accountGeneration
        let cursor = useRetryCursor ? retryCursor : (reset ? nil : conversationListCursor)
        isLoadingConversations = true
        defer {
            if accountGeneration == accountRevision {
                isLoadingConversations = false
            }
        }

        do {
            let response: ChatConversationListResponse = try await client.send(
                .myConversations(after: cursor, limit: 50)
            )
            guard accountGeneration == accountRevision else { return }
            if reset {
                conversations = response.results
            } else {
                var seen = Set(conversations.map(\.id))
                conversations.append(contentsOf: response.results.filter { seen.insert($0.id).inserted })
            }
            conversationListCursor = response.pageInfo.endCursor
            hasMoreConversations = response.pageInfo.hasNextPage && response.pageInfo.endCursor != nil
            clearLoadFailureIfMatching(.conversations(reset: reset, after: cursor))
        } catch {
            guard accountGeneration == accountRevision else { return }
            let operation = AndroidTranscriptLoadFailure.conversations(reset: reset, after: cursor)
            failedConversationListLoad = AndroidTranscriptLoadFailureRecord(
                operation: operation,
                message: error.localizedDescription
            )
            latestTranscriptLoadFailure = operation
            restoreSelectedConversationError()
        }
    }

    func selectConversation(_ id: String, revision: UUID = UUID()) async {
        guard !accountExitIsInProgress else { return }
        guard selectedConversationID != id else { return }
        if let previousID = selectedConversationID,
           let draftBackedFailure = draftBackedGenerationFailures.removeValue(forKey: previousID) {
            failedGenerations[previousID] = draftBackedFailure
        }
        selectedConversationID = id
        conversationCreationFailed = false
        conversationCreationErrorMessage = nil
        conversationGeneration = revision
        messages = []
        isLoadingMessages = false
        isLoadingOlderMessages = false
        loadedConversationHistoryID = nil
        messagePageCursor = nil
        hasOlderMessages = false
        if let failure = failedConversationHistoryLoad,
           case let .messages(failedID, _, _) = failure.operation,
           failedID != id {
            failedConversationHistoryLoad = nil
            if latestTranscriptLoadFailure == failure.operation {
                latestTranscriptLoadFailure = failedConversationListLoad?.operation
            }
        }
        restoreSelectedConversationError()
        await loadConversationMessages(reset: true)
    }

    func loadConversationMessages(
        reset: Bool,
        retryCursor: String? = nil,
        useRetryCursor: Bool = false
    ) async {
        guard let client = apiClient, let conversationID = selectedConversationID,
              !accountExitIsInProgress,
              !isLoadingMessages, reset || (!isGenerating && hasOlderMessages)
        else { return }
        let accountRevision = accountGeneration
        let conversationRevision = conversationGeneration
        let cursor = useRetryCursor ? retryCursor : (reset ? nil : messagePageCursor)
        let replaceableMessageIDs = replaceableTranscriptMessageIDs
        if reset {
            loadedConversationHistoryID = nil
        }
        isLoadingMessages = true
        isLoadingOlderMessages = !reset
        defer {
            if accountGeneration == accountRevision, conversationGeneration == conversationRevision {
                isLoadingMessages = false
                isLoadingOlderMessages = false
            }
        }

        do {
            let response: ChatMessagesResponse = try await client.send(
                .myConversationMessages(conversationId: conversationID, after: cursor, limit: 50)
            )
            guard accountGeneration == accountRevision,
                  conversationGeneration == conversationRevision,
                  selectedConversationID == conversationID
            else { return }
            try await applyConversationMessages(
                response,
                conversationID: conversationID,
                reset: reset,
                cursor: cursor,
                replaceableMessageIDs: replaceableMessageIDs
            )
        } catch {
            guard accountGeneration == accountRevision,
                  conversationGeneration == conversationRevision
            else { return }
            recordConversationMessagesLoadFailure(
                error,
                conversationID: conversationID,
                reset: reset,
                cursor: cursor
            )
        }
    }

    private func recordConversationMessagesLoadFailure(
        _ error: Error,
        conversationID: String,
        reset: Bool,
        cursor: String?
    ) {
        let operation = AndroidTranscriptLoadFailure.messages(
            conversationID: conversationID,
            reset: reset,
            after: cursor
        )
        failedConversationHistoryLoad = AndroidTranscriptLoadFailureRecord(
            operation: operation,
            message: error.localizedDescription
        )
        latestTranscriptLoadFailure = operation
        appendPendingTurnIfNeeded(for: conversationID)
        restoreSelectedConversationError()
    }

    private func makeChatMessage(from message: ChatMessage) -> AndroidChatMessage? {
        AndroidChatMessage(transcriptMessage: message)
    }

    private func uniqueMessages(_ messages: [AndroidChatMessage]) -> [AndroidChatMessage] {
        var seen = Set<String>()
        return messages.filter { seen.insert($0.id).inserted }
    }

    private func applyConversationMessages(
        _ response: ChatMessagesResponse,
        conversationID: String,
        reset: Bool,
        cursor: String?,
        replaceableMessageIDs: Set<String>
    ) async throws {
        let accountRevision = accountGeneration
        let conversationRevision = conversationGeneration
        let acknowledgedMessageIDs = try await acknowledgePendingTurnIfPresent(
            in: response.results,
            conversationID: conversationID,
            reset: reset
        )
        guard accountGeneration == accountRevision,
              conversationGeneration == conversationRevision,
              selectedConversationID == conversationID else { return }
        let loaded = uniqueMessages(response.results.compactMap { makeChatMessage(from: $0) })
        if reset {
            let addedDuringRequest = messages.filter { !replaceableMessageIDs.contains($0.id) }
            messages = uniqueMessages(loaded + addedDuringRequest)
            appendPendingTurnIfNeeded(for: conversationID)
        } else {
            messages.removeAll { acknowledgedMessageIDs.contains($0.id) }
            var existing = Set(messages.map(\.id))
            let older = loaded.filter { existing.insert($0.id).inserted }
            messages = older + messages
        }
        sortMessagesChronologically()
        messagePageCursor = response.pageInfo.endCursor
        hasOlderMessages = response.pageInfo.hasNextPage && response.pageInfo.endCursor != nil
        if reset {
            loadedConversationHistoryID = conversationID
        }
        clearLoadFailureIfMatching(.messages(conversationID: conversationID, reset: reset, after: cursor))
    }
}

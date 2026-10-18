import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension ViewModel {
    func bindAccount(client: APIClient?, userID: String?) async {
        guard currentAccountUserID != userID || apiClient !== client else { return }
        resetForAccountTransition()
        currentAccountUserID = userID
        apiClient = userID == nil ? nil : client
        guard userID != nil, apiClient != nil else { return }

        await loadConversationPage(reset: true)
        if selectedConversationID == nil, let first = conversations.first {
            await selectConversation(first.id)
        }
    }

    func loadConversationPage(
        reset: Bool,
        retryCursor: String? = nil,
        useRetryCursor: Bool = false
    ) async {
        guard let client = apiClient, currentAccountUserID != nil,
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
            applyConversationMessages(
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
    }

    private var replaceableTranscriptMessageIDs: Set<String> {
        let pending = pendingTurnForSelectedConversation
        let protectedIDs = [pending?.userMessageID, pending?.assistantMessageID].compactMap { $0 }
        return Set(messages.map(\.id)).subtracting(protectedIDs)
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
    ) {
        let acknowledgedMessageIDs = acknowledgePendingTurnIfPresent(
            in: response.results,
            conversationID: conversationID,
            reset: reset
        )
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

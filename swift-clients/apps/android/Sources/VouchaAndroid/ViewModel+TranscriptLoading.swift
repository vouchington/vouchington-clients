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

    func loadMoreConversations() async {
        await loadConversationPage(reset: false)
    }

    func retryConversationLoad() async {
        if selectedConversationID != nil {
            await loadConversationMessages(reset: true)
            return
        }
        await loadConversationPage(reset: true)
        if selectedConversationID == nil, let first = conversations.first {
            await selectConversation(first.id)
        }
    }

    private func loadConversationPage(reset: Bool) async {
        guard let client = apiClient, currentAccountUserID != nil,
              !isLoadingConversations, reset || hasMoreConversations
        else { return }
        let accountRevision = accountGeneration
        let cursor = reset ? nil : conversationListCursor
        isLoadingConversations = true
        defer {
            if accountGeneration == accountRevision { isLoadingConversations = false }
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
            errorMessage = nil
            transcriptLoadFailed = false
        } catch {
            guard accountGeneration == accountRevision else { return }
            errorMessage = error.localizedDescription
            transcriptLoadFailed = true
        }
    }

    func selectConversation(_ id: String) async {
        guard selectedConversationID != id else { return }
        selectedConversationID = id
        conversationGeneration = UUID()
        messages = []
        isLoadingMessages = false
        isLoadingOlderMessages = false
        messagePageCursor = nil
        hasOlderMessages = false
        errorMessage = nil
        await loadConversationMessages(reset: true)
    }

    func createConversation() async {
        guard !isCreatingConversation, !isLoadingConversations, let client = apiClient,
              let userID = currentAccountUserID
        else { return }
        let accountRevision = accountGeneration
        isCreatingConversation = true
        defer {
            if accountGeneration == accountRevision { isCreatingConversation = false }
        }
        do {
            let response: ChatConversationResponse = try await client.send(.createConversation())
            guard accountGeneration == accountRevision, currentAccountUserID == userID else { return }
            if !conversations.contains(where: { $0.id == response.conversation.id }) {
                conversations.insert(response.conversation, at: 0)
            }
            await selectConversation(response.conversation.id)
        } catch {
            guard accountGeneration == accountRevision else { return }
            errorMessage = error.localizedDescription
        }
    }

    func loadOlderMessages() async {
        await loadConversationMessages(reset: false)
    }

    private func loadConversationMessages(reset: Bool) async {
        guard let client = apiClient, let conversationID = selectedConversationID,
              !isLoadingMessages, reset || hasOlderMessages
        else { return }
        let accountRevision = accountGeneration
        let conversationRevision = conversationGeneration
        let cursor = reset ? nil : messagePageCursor
        isLoadingMessages = true
        if !reset { isLoadingOlderMessages = true }
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
            let loaded = uniqueMessages(response.results.compactMap { makeChatMessage(from: $0) })
            if reset {
                messages = loaded
                appendPendingTurnIfNeeded(for: conversationID)
            } else {
                var existing = Set(messages.map(\.id))
                let older = loaded.filter { existing.insert($0.id).inserted }
                messages = older + messages
            }
            sortMessagesChronologically()
            messagePageCursor = response.pageInfo.endCursor
            hasOlderMessages = response.pageInfo.hasNextPage && response.pageInfo.endCursor != nil
            errorMessage = nil
            transcriptLoadFailed = false
        } catch {
            guard accountGeneration == accountRevision,
                  conversationGeneration == conversationRevision
            else { return }
            errorMessage = error.localizedDescription
            transcriptLoadFailed = true
        }
    }

    private func makeChatMessage(from message: ChatMessage) -> AndroidChatMessage? {
        let role = message.content.role == "assistant" ? "assistant" : "user"
        return AndroidChatMessage(
            id: message.id,
            role: role,
            content: message.content.displayText,
            createdAt: message.createdAt
        )
    }

    private func uniqueMessages(_ messages: [AndroidChatMessage]) -> [AndroidChatMessage] {
        var seen = Set<String>()
        return messages.filter { seen.insert($0.id).inserted }
    }

    func sortMessagesChronologically() {
        messages.sort {
            if $0.createdAt == $1.createdAt { return $0.id < $1.id }
            return $0.createdAt < $1.createdAt
        }
    }

    func appendPendingTurnIfNeeded(for conversationID: String) {
        guard let pending = pendingTurns[conversationID] else { return }
        if !messages.contains(where: { $0.id == pending.userMessageID }) {
            messages.append(.init(id: pending.userMessageID, role: "user", content: pending.userText))
        }
        if !messages.contains(where: { $0.id == pending.assistantMessageID }) {
            messages.append(.init(id: pending.assistantMessageID, role: "assistant", content: pending.assistantText))
        }
        sortMessagesChronologically()
    }
}

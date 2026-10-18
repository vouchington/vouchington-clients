import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

extension ViewModel {
    @discardableResult
    func createConversation() async -> Bool {
        guard !isCreatingConversation, !isLoadingConversations,
              !pendingStoreIsLoading, !pendingStoreLoadFailed, !accountExitIsInProgress,
              let client = apiClient,
              let userID = currentAccountUserID
        else { return false }
        let accountRevision = accountGeneration
        let selectionRevision = conversationGeneration
        conversationCreationFailed = false
        conversationCreationErrorMessage = nil
        errorMessage = nil
        isCreatingConversation = true
        defer {
            if accountGeneration == accountRevision { isCreatingConversation = false }
        }
        do {
            let response: ChatConversationResponse = try await client.send(.createConversation())
            guard accountGeneration == accountRevision, currentAccountUserID == userID else { return false }
            if !conversations.contains(where: { $0.id == response.conversation.id }) {
                conversations.insert(response.conversation, at: 0)
            }
            guard conversationGeneration == selectionRevision else { return false }
            conversationCreationFailed = false
            conversationCreationErrorMessage = nil
            let createdSelectionRevision = UUID()
            await selectConversation(response.conversation.id, revision: createdSelectionRevision)
            return conversationGeneration == createdSelectionRevision &&
                selectedConversationID == response.conversation.id &&
                loadedConversationHistoryID == response.conversation.id
        } catch {
            guard accountGeneration == accountRevision,
                  conversationGeneration == selectionRevision else { return false }
            conversationCreationFailed = true
            conversationCreationErrorMessage = error.localizedDescription
            errorMessage = error.localizedDescription
            return false
        }
    }

}

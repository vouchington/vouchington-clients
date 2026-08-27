import Foundation
import VouchaAPI
import VouchaModels

extension NativeChatViewModel {
    var normalizedDraftMessage: String? {
        let trimmed = draftMessage.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    func ensureConversationForDraftSend(
        client: APIClient
    ) async throws -> (conversationId: String, created: Bool)? {
        if let selectedConversationId {
            return (selectedConversationId, false)
        }

        let expectedSelectionRevision = selectionRevision
        let conversation = try await createConversationForSend(client: client)
        guard selectionRevision == expectedSelectionRevision, selectedConversationId == nil else {
            return nil
        }

        conversations.removeAll { $0.id == conversation.id }
        conversations.insert(conversation, at: 0)
        selectedConversationId = conversation.id
        selectionRevision += 1
        loadedConversationDetailId = nil
        conversationTitleDraft = conversation.title
        return (conversation.id, true)
    }
}

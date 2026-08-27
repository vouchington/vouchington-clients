import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeChatViewModel {
    func loadOlderMessages() async {
        guard
            let client,
            let id = selectedConversationId,
            let after = detailPageInfo?.endCursor,
            canLoadOlderMessages
        else { return }
        let revision = selectionRevision
        olderMessagesLoadRevision += 1
        let pageRevision = olderMessagesLoadRevision
        isLoadingOlderMessages = true
        defer {
            if olderMessagesLoadRevision == pageRevision {
                isLoadingOlderMessages = false
            }
        }
        do {
            let response: ChatMessagesResponse = try await client.send(
                .myConversationMessages(conversationId: id, after: after)
            )
            guard isCurrentOlderMessagesPage(id: id, selection: revision, page: pageRevision) else { return }
            var existingIds = Set(messages.map(\.id))
            let olderMessages = response.results
                .map { makeTimelineMessage(from: $0) }
                .filter { existingIds.insert($0.id).inserted }
            messages = olderMessages + messages
            detailPageInfo = .init(
                hasNextPage: response.pageInfo.hasNextPage,
                endCursor: response.pageInfo.endCursor
            )
            detailErrorMessage = nil
        } catch let error as VouchaError {
            guard isCurrentOlderMessagesPage(id: id, selection: revision, page: pageRevision) else { return }
            detailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard isCurrentOlderMessagesPage(id: id, selection: revision, page: pageRevision) else { return }
            detailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    private func isCurrentOlderMessagesPage(id: String, selection: Int, page: Int) -> Bool {
        selectedConversationId == id &&
            selectionRevision == selection &&
            olderMessagesLoadRevision == page
    }

    func mergeConversationList(
        existing: [ChatConversation],
        refreshed: [ChatConversation]
    ) -> [ChatConversation] {
        var merged = refreshed
        let refreshedIds = Set(refreshed.map(\.id))
        let preservedIds = Set(([selectedConversationId].compactMap { $0 }) + Array(createdConversationIds))
        for conversation in existing
            where preservedIds.contains(conversation.id) && refreshedIds.contains(conversation.id) == false {
            merged.append(conversation)
        }
        return merged
    }
}

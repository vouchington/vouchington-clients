import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeChatViewModel {
    func loadMoreConversations() async {
        await loadConversations(reset: false)
    }

    func loadConversations(reset: Bool) async {
        guard let client else { return }
        let revision = reset ? (activeConversationListLoadRevision + 1) : activeConversationListLoadRevision
        let existing = conversations
        if reset {
            activeConversationListLoadRevision = revision
            conversationListPageRequestRevision += 1
            isLoadingList = true
            listErrorMessage = nil
            conversationPagination.reset()
        }
        guard let request = conversationPagination.beginNextPage() else { return }
        defer {
            if activeConversationListLoadRevision == revision {
                isLoadingList = false
            }
        }

        do {
            let response: ChatConversationListResponse = try await client.send(.myConversations(after: request.cursor))
            guard activeConversationListLoadRevision == revision,
                  conversationPagination.isCurrent(request)
            else { return }
            let incoming = reset
                ? mergeConversationList(existing: existing, refreshed: response.results)
                : response.results
            conversationPagination.complete(
                request,
                items: incoming,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            guard activeConversationListLoadRevision == revision else { return }
            conversationPagination.fail(request, error: error)
            listErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard activeConversationListLoadRevision == revision else { return }
            conversationPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            listErrorMessage = .verbatim(error.localizedDescription)
        }
    }
}

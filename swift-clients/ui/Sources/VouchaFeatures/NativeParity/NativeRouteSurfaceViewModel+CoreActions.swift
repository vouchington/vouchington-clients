import VouchaAPI
import VouchaCore

public extension NativeRouteSurfaceViewModel {
    var canLoadOlderAgentConversationMessages: Bool {
        agentConversationPageInfo?.hasNextPage == true && !isLoadingOlderAgentConversationMessages
    }

    var canLoadMoreAgents: Bool {
        agentDirectoryPageInfo?.hasNextPage == true && !isLoadingMoreAgents
    }

    var canLoadMoreAgentConversations: Bool {
        !isLoadingAgentConversations
            && (agentConversationListPageInfo?.hasNextPage == true
                || agentConversationsPageErrorMessage != nil)
    }

    var isLoadingAgentConversations: Bool {
        agentConversationListPagination.isLoading
    }

    func perform(action: NativeRouteSurfaceAction) async {
        guard let client, !isLoading else { return }

        state = .loading
        do {
            let loadedRows: [NativeRouteDestinationRow] = switch action {
            case .compareTopics:
                try await loadTopicCompareRows(client: client)
            case .compareHostnames:
                try await loadHostnameCompareRows(client: client)
            }
            rows = loadedRows
            state = .loaded
        } catch let error as VouchaError {
            state = .error(error)
        } catch {
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }
}

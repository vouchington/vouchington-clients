import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadAgentRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        if let agentId = routeMatch?.param("idOrSlug") {
            return try await loadAgentDetailRows(client: client, agentId: agentId)
        }
        agentDirectoryLoadRevision += 1
        let revision = agentDirectoryLoadRevision
        agentDirectoryPageRequestRevision += 1
        let pageRevision = agentDirectoryPageRequestRevision
        agentDirectoryPaginationErrorMessage = nil
        agentDirectoryPagination.reset()
        agentDirectoryUsers = [:]
        guard let request = agentDirectoryPagination.beginNextPage() else { return rows }
        let response: AgentListResponse = try await client.send(
            .agents(after: request.cursor, limit: agentListPageSize)
        )
        guard revision == agentDirectoryLoadRevision,
              pageRevision == agentDirectoryPageRequestRevision,
              routeMatch?.path == "/agents"
        else { return rows }
        agentDirectoryPagination.complete(
            request,
            items: response.results,
            endCursor: response.pageInfo.endCursor,
            hasNextPage: response.pageInfo.hasNextPage
        )
        agentDirectoryUsers = response.users
        return agentDirectoryRows()
    }

    private func loadAgentDetailRows(client: APIClient, agentId: String) async throws
        -> [NativeRouteDestinationRow] {
        if let conversationId = routeMatch?.param("conversationId") {
            return try await loadAgentConversationRows(
                client: client,
                agentId: agentId,
                conversationId: conversationId
            )
        }

        hydrateAgentConversationFilterFromRoute()

        async let agentEnvelope: AgentDetailResponse = client.send(.agent(idOrSlug: agentId))
        agentConversationListLoadRevision += 1
        let revision = agentConversationListLoadRevision
        agentConversationListPageRequestRevision += 1
        let pageRevision = agentConversationListPageRequestRevision
        agentConversationsPageErrorMessage = nil
        agentConversationListPagination.reset()
        agentConversationListUsers = [:]
        guard let request = agentConversationListPagination.beginNextPage() else { return rows }
        async let conversations: AgentConversationListResponse = client.send(
            .agentConversations(agentIdOrSlug: agentId, filter: agentConversationFilter, limit: agentListPageSize)
        )
        let agent = try await agentEnvelope
        let loadedConversations = try await conversations
        guard revision == agentConversationListLoadRevision,
              pageRevision == agentConversationListPageRequestRevision,
              routeMatch?.param("idOrSlug") == agentId,
              routeMatch?.param("conversationId") == nil
        else { return rows }
        agentConversationListPagination.complete(
            request,
            items: loadedConversations.results,
            endCursor: loadedConversations.pageInfo.endCursor,
            hasNextPage: loadedConversations.pageInfo.hasNextPage
        )
        agentConversationListUsers = loadedConversations.users
        agentDetail = agent
        return agentConversationListRows()
    }

    func loadMoreAgents() async {
        guard canLoadMoreAgents, let client,
              let request = agentDirectoryPagination.beginNextPage()
        else { return }
        let revision = agentDirectoryLoadRevision
        agentDirectoryPageRequestRevision += 1
        let pageRevision = agentDirectoryPageRequestRevision
        do {
            let response: AgentListResponse = try await client.send(
                .agents(after: request.cursor, limit: agentListPageSize)
            )
            guard isCurrentAgentPage(revision: revision, pageRevision: pageRevision),
                  agentDirectoryPagination.isCurrent(request)
            else { return }
            agentDirectoryPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            agentDirectoryUsers.merge(response.users) { _, incoming in incoming }
            agentDirectoryPaginationErrorMessage = nil
            rows = agentDirectoryRows()
        } catch {
            guard isCurrentAgentPage(revision: revision, pageRevision: pageRevision) else { return }
            agentDirectoryPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            agentDirectoryPaginationErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func loadMoreAgentConversations() async {
        guard canLoadMoreAgentConversations,
              let client,
              let agentId = routeMatch?.param("idOrSlug"),
              let request = agentConversationListPagination.beginNextPage()
        else { return }
        let revision = agentConversationListLoadRevision
        agentConversationListPageRequestRevision += 1
        let pageRevision = agentConversationListPageRequestRevision
        do {
            let response: AgentConversationListResponse = try await client.send(
                .agentConversations(
                    agentIdOrSlug: agentId,
                    filter: agentConversationFilter,
                    after: request.cursor,
                    limit: agentListPageSize
                )
            )
            guard isCurrentAgentConversationPage(agentId, revision: revision, pageRevision: pageRevision),
                  agentConversationListPagination.isCurrent(request)
            else { return }
            agentConversationListPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            agentConversationListUsers.merge(response.users) { _, incoming in incoming }
            agentConversationsPageErrorMessage = nil
            rows = agentConversationListRows()
        } catch {
            guard isCurrentAgentConversationPage(agentId, revision: revision, pageRevision: pageRevision)
            else { return }
            agentConversationListPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            agentConversationsPageErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func reloadAgentConversations(filter: AgentConversationFilter?) async {
        guard let client, let agentId = routeMatch?.param("idOrSlug") else { return }
        agentConversationListLoadRevision += 1
        let revision = agentConversationListLoadRevision
        agentConversationListPageRequestRevision += 1
        let pageRevision = agentConversationListPageRequestRevision
        agentConversationFilter = filter?.trimmed
        agentConversationsPageErrorMessage = nil
        agentConversationListPagination.reset()
        agentConversationListUsers = [:]
        guard let request = agentConversationListPagination.beginNextPage() else { return }
        do {
            let response: AgentConversationListResponse = try await client.send(
                .agentConversations(agentIdOrSlug: agentId, filter: agentConversationFilter, limit: agentListPageSize)
            )
            guard isCurrentAgentConversationPage(agentId, revision: revision, pageRevision: pageRevision),
                  agentConversationListPagination.isCurrent(request)
            else { return }
            agentConversationListPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
            agentConversationListUsers = response.users
            rows = agentConversationListRows()
        } catch {
            guard isCurrentAgentConversationPage(agentId, revision: revision, pageRevision: pageRevision)
            else { return }
            agentConversationListPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            agentConversationsPageErrorMessage = .verbatim(error.localizedDescription)
        }
    }

}

import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadAgentConversationRows(
        client: APIClient,
        agentId: String,
        conversationId: String
    ) async throws -> [NativeRouteDestinationRow] {
        agentConversationLoadRevision += 1
        let loadRevision = agentConversationLoadRevision
        agentConversationPageRequestRevision += 1
        let pageRequestRevision = agentConversationPageRequestRevision
        agentConversationContinuationToken += 1
        activeAgentConversationContinuationToken = nil
        agentConversationPaginationErrorMessage = nil
        agentConversationAgentSystemUserId = nil
        async let agentDetailResponse: AgentDetailResponse = client.send(.agent(idOrSlug: agentId))
        async let conversationResponse: AgentConversationDetailResponse = client.send(.agentConversation(
            agentIdOrSlug: agentId,
            conversationId: conversationId,
            limit: agentMessagePageSize
        ))
        let (agentDetail, response) = try await (agentDetailResponse, conversationResponse)
        guard loadRevision == agentConversationLoadRevision,
              pageRequestRevision == agentConversationPageRequestRevision,
              routeMatch?.param("idOrSlug") == agentId,
              routeMatch?.param("conversationId") == conversationId
        else {
            return rows
        }
        agentConversation = response.conversation
        agentConversationAgentSystemUserId = agentDetail.agent.systemUserId
        agentConversationMessages = response.results
        agentConversationPageInfo = response.pageInfo
        return agentConversationRows(fallbackId: conversationId, agentSystemUserId: agentDetail.agent.systemUserId)
    }

    func loadOlderAgentConversationMessages() async {
        guard canLoadOlderAgentConversationMessages,
              let client,
              let agentId = routeMatch?.param("idOrSlug"),
              let conversationId = routeMatch?.param("conversationId"),
              let cursor = agentConversationPageInfo?.endCursor
        else { return }

        let revision = agentConversationLoadRevision
        agentConversationPageRequestRevision += 1
        let pageRequestRevision = agentConversationPageRequestRevision
        agentConversationContinuationToken += 1
        let continuationRequestToken = agentConversationContinuationToken
        activeAgentConversationContinuationToken = continuationRequestToken
        defer {
            if activeAgentConversationContinuationToken == continuationRequestToken {
                activeAgentConversationContinuationToken = nil
            }
        }
        do {
            let response: AgentConversationDetailResponse = try await client.send(.agentConversation(
                agentIdOrSlug: agentId,
                conversationId: conversationId,
                after: cursor,
                limit: agentMessagePageSize
            ))
            guard revision == agentConversationLoadRevision,
                  pageRequestRevision == agentConversationPageRequestRevision,
                  continuationRequestToken == agentConversationContinuationToken,
                  routeMatch?.param("idOrSlug") == agentId,
                  routeMatch?.param("conversationId") == conversationId
            else { return }
            var seenIds = Set(agentConversationMessages.map(\.id))
            let olderMessages = response.results.filter { seenIds.insert($0.id).inserted }
            agentConversationMessages = olderMessages + agentConversationMessages
            agentConversationPageInfo = response.pageInfo
            agentConversationPaginationErrorMessage = nil
            rows = agentConversationRows(
                fallbackId: conversationId,
                agentSystemUserId: agentConversationAgentSystemUserId
            )
        } catch {
            guard revision == agentConversationLoadRevision,
                  pageRequestRevision == agentConversationPageRequestRevision,
                  continuationRequestToken == agentConversationContinuationToken
            else { return }
            agentConversationPaginationErrorMessage = .verbatim(error.localizedDescription)
        }
    }
}

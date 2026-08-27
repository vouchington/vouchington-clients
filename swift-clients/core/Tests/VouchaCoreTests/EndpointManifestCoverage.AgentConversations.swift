import VouchaAPI

extension EndpointManifestCoverage {
    private static let agentConversationId = "00000000-0000-7000-8000-000000000101"
    private static let agentMessagesAfter =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoie1wiYWdlbnRJZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMzAyXCIsXCJjb252ZXJzYXRpb25JZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMTAxXCIsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9"
    private static let agentsAfter =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDMwMSIsInNjb3BlIjoiYWdlbnQtZGlyZWN0b3J5OmlkLWRlc2MifQ"
    private static let conversationsAfter =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMTAxMSIsInNjb3BlIjoie1wiYWdlbnRTeXN0ZW1Vc2VySWRcIjpcIjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMVwiLFwidXNlcklkXCI6bnVsbCxcInBvc3RJZFwiOm51bGwsXCJyc3NGZWVkSXRlbUlkXCI6bnVsbCxcIm9ubHlMaW5rZWRcIjp0cnVlLFwib3JkZXJcIjpcImlkLWRlc2NcIn0ifQ"

    static let nativeAgentConversationEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.agents.default") { Endpoint.agents(limit: 2) },
        ManifestRegisteredEndpoint(id: "native.agents.page-2") {
            Endpoint.agents(after: agentsAfter, limit: 2)
        },
        ManifestRegisteredEndpoint(id: "native.agents.detail.default") { Endpoint.agent(idOrSlug: "helper") },
        ManifestRegisteredEndpoint(id: "native.agents.conversations.default") {
            Endpoint.agentConversations(agentIdOrSlug: "helper", limit: 2)
        },
        ManifestRegisteredEndpoint(id: "native.agents.conversations.page-2") {
            Endpoint.agentConversations(agentIdOrSlug: "helper", after: conversationsAfter, limit: 2)
        },
        ManifestRegisteredEndpoint(id: "native.agents.conversation.default") {
            Endpoint.agentConversation(
                agentIdOrSlug: "helper",
                conversationId: agentConversationId,
                limit: 2
            )
        },
        ManifestRegisteredEndpoint(id: "native.agents.conversations.filtered-username") {
            Endpoint.agentConversations(
                agentIdOrSlug: "helper",
                filter: .username("fixture-agent-user-011"),
                limit: 2
            )
        },
        ManifestRegisteredEndpoint(id: "native.agents.conversation.page-2") {
            Endpoint.agentConversation(
                agentIdOrSlug: "helper",
                conversationId: agentConversationId,
                after: agentMessagesAfter,
                limit: 2
            )
        }
    ]
}

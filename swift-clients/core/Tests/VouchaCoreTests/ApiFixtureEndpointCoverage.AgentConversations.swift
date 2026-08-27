@testable import VouchaAPI

extension ApiFixtureEndpointCoverageTests {
    static let agentConversationFixtureEndpoints: [String: Endpoint] = [
        "native.agents.default": .agents(limit: 2),
        "native.agents.page-2": .agents(after: agentsAfter, limit: 2),
        "native.agents.detail.default": .agent(idOrSlug: "helper"),
        "native.agents.conversations.default": .agentConversations(agentIdOrSlug: "helper", limit: 2),
        "native.agents.conversations.page-2": .agentConversations(
            agentIdOrSlug: "helper",
            after: conversationsAfter,
            limit: 2
        ),
        "native.agents.conversations.filtered-username": .agentConversations(
            agentIdOrSlug: "helper",
            filter: .username("fixture-agent-user-011"),
            limit: 2
        ),
        "native.agents.conversation.default": .agentConversation(
            agentIdOrSlug: "helper",
            conversationId: conversationId,
            limit: 2
        ),
        "native.agents.conversation.page-2": .agentConversation(
            agentIdOrSlug: "helper",
            conversationId: conversationId,
            after: messagesAfter,
            limit: 2
        )
    ]

    private static let conversationId = "00000000-0000-7000-8000-000000000101"
    private static let messagesAfter =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoie1wiYWdlbnRJZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMzAyXCIsXCJjb252ZXJzYXRpb25JZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMTAxXCIsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9"
    private static let agentsAfter =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDMwMSIsInNjb3BlIjoiYWdlbnQtZGlyZWN0b3J5OmlkLWRlc2MifQ"
    private static let conversationsAfter =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMTAxMSIsInNjb3BlIjoie1wiYWdlbnRTeXN0ZW1Vc2VySWRcIjpcIjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMVwiLFwidXNlcklkXCI6bnVsbCxcInBvc3RJZFwiOm51bGwsXCJyc3NGZWVkSXRlbUlkXCI6bnVsbCxcIm9ubHlMaW5rZWRcIjp0cnVlLFwib3JkZXJcIjpcImlkLWRlc2NcIn0ifQ"
}

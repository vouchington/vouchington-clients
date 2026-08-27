@testable import VouchaAPI
import VouchaModels
import XCTest

final class EndpointEngineeringAgentsCoverageTests: XCTestCase {
    func testAgentListEndpointsForwardOpaqueCursors() {
        XCTAssertEqual(Endpoint.agents(after: "opaque", limit: 2).queryItems.last?.value, "opaque")
        let conversations = Endpoint.agentConversations(
            agentIdOrSlug: "helper agent",
            after: "opaque",
            limit: 2
        )
        XCTAssertEqual(conversations.path, "/api/v1/agents/helper%20agent/conversations")
        XCTAssertEqual(conversations.queryItems.last?.value, "opaque")
    }

    func testAgentConversationEndpointForwardsOpaqueCursorAndLimit() {
        let endpoint = Endpoint.agentConversation(
            agentIdOrSlug: "helper agent",
            conversationId: "conversation/id",
            after: "opaque+cursor=",
            limit: 17
        )

        XCTAssertEqual(endpoint.method, .GET)
        XCTAssertEqual(endpoint.path, "/api/v1/agents/helper%20agent/conversations/conversation%2Fid")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "17"),
            URLQueryItem(name: "after", value: "opaque+cursor=")
        ])
    }

    func testAgentDetailAndFiltersSerializeOneEscapedQueryItem() {
        let detail = Endpoint.agent(idOrSlug: "a/b:c% d😀")
        XCTAssertEqual(detail.path, "/api/v1/agents/a%2Fb%3Ac%25%20d%F0%9F%98%80")

        let filters: [(AgentConversationFilter, String)] = [
            (.userId("user/1"), "user_id"),
            (.username("name: % 😀"), "username"),
            (.postId("post/1"), "post_id"),
            (.postSlug("post: % 😀"), "post_slug"),
            (.rssFeedItemId("feed/1"), "rss_feed_item_id")
        ]
        for (filter, expectedName) in filters {
            let endpoint = Endpoint.agentConversations(agentIdOrSlug: "helper", filter: filter, limit: 2)
            XCTAssertEqual(endpoint.queryItems, [
                URLQueryItem(name: "limit", value: "2"),
                URLQueryItem(name: expectedName, value: filter.value)
            ])
        }
    }

    func testAgentEndpointsEncodeRawRouteSegmentsOnce() {
        XCTAssertEqual(Endpoint.agent(idOrSlug: "foo bar").path, "/api/v1/agents/foo%20bar")
        XCTAssertEqual(
            Endpoint.agentConversations(agentIdOrSlug: "foo bar").path,
            "/api/v1/agents/foo%20bar/conversations"
        )
        XCTAssertEqual(
            Endpoint.agentConversation(agentIdOrSlug: "foo bar", conversationId: "conversation/one").path,
            "/api/v1/agents/foo%20bar/conversations/conversation%2Fone"
        )
    }

    func testFilteredConversationContinuationPreservesFilterAndCursor() {
        let endpoint = Endpoint.agentConversations(
            agentIdOrSlug: "helper",
            filter: .username(" fixture "),
            after: "opaque+cursor=",
            limit: 2
        )
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "2"),
            URLQueryItem(name: "after", value: "opaque+cursor="),
            URLQueryItem(name: "username", value: "fixture")
        ])
        XCTAssertNil(AgentConversationFilter(kind: .username, value: "  "))
    }

    func testAgentDetailDecodesExplicitNullUser() throws {
        let data = Data(
            #"{"agent":{"id":"agent","system_user_id":"system","agent_type":"helper","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":null,"moderator":null},"user":null}"#
                .utf8
        )
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(AgentDetailResponse.self, from: data)
        XCTAssertNil(response.user)
        XCTAssertNil(response.agent.slug)
        XCTAssertNil(response.agent.moderator)
    }
}

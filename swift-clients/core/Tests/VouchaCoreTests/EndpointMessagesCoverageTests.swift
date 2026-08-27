import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointMessagesCoverageTests: XCTestCase {
    func testMyMessageEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.myMessageConversation(conversationId: "conv-1"),
            path: "/api/v1/my/messages/conv-1"
        )

        assertEndpoint(
            Endpoint.createMyMessageConversation(userId: "user-1"),
            method: .POST,
            path: "/api/v1/my/messages",
            body: ["user_id": "user-1"]
        )

        assertEndpoint(
            Endpoint.createMyMessageConversation(userIds: ["user-1", "user-2"]),
            method: .POST,
            path: "/api/v1/my/messages",
            body: ["user_ids": ["user-1", "user-2"]]
        )

        assertEndpoint(
            Endpoint.sendMyMessageConversationMessage(conversationId: "conv-1", text: "Hello"),
            method: .POST,
            path: "/api/v1/my/messages/conv-1/messages",
            body: ["text": "Hello"]
        )

        assertEndpoint(
            Endpoint.myMessageConversationParticipants(conversationId: "conv-1"),
            path: "/api/v1/my/messages/conv-1/participants"
        )

        assertEndpoint(
            Endpoint.addMyMessageConversationParticipant(conversationId: "conv-1", userId: "user-2"),
            method: .POST,
            path: "/api/v1/my/messages/conv-1/participants",
            body: ["user_id": "user-2"]
        )

        assertEndpoint(
            Endpoint.removeMyMessageConversationParticipant(conversationId: "conv-1", userId: "user-2"),
            method: .DELETE,
            path: "/api/v1/my/messages/conv-1/participants/user-2"
        )

        assertEndpoint(
            Endpoint.updateMyMessageConversationParticipantAddPolicy(
                conversationId: "conv-1",
                policy: .allMembers
            ),
            method: .PATCH,
            path: "/api/v1/my/messages/conv-1",
            body: ["participant_add_policy": "all_members"]
        )

        let search = Endpoint.myMessageUserSearch(query: "bo", limit: 10)
        XCTAssertEqual(search.path, "/api/v1/users")
        XCTAssertEqual(search.queryItems, [
            URLQueryItem(name: "q", value: "bo"),
            URLQueryItem(name: "limit", value: "10")
        ])

        let searchNextPage = Endpoint.myMessageUserSearch(query: "bo", after: "cursor-1", limit: 10)
        XCTAssertEqual(searchNextPage.queryItems, [
            URLQueryItem(name: "q", value: "bo"),
            URLQueryItem(name: "after", value: "cursor-1"),
            URLQueryItem(name: "limit", value: "10")
        ])
    }
}

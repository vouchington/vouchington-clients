import Foundation
@testable import VouchaAPI
import XCTest

final class FollowerDistributionEndpointTests: XCTestCase {
    func testShareEndpointsHaveNoRequestBody() {
        assertEndpoint(
            .sharePostWithFollowers(postId: "post / id"),
            method: .POST,
            path: "/api/v1/posts/post%20%2F%20id/shares"
        )
        assertEndpoint(
            .shareRssFeedItemWithFollowers(rssFeedItemId: "item / id"),
            method: .POST,
            path: "/api/v1/rss-feed-items/item%20%2F%20id/shares"
        )
    }

    func testSendEndpointsEncodeSelectedRecipientIds() throws {
        let request = try FollowerDistributionRequest(selectedRecipientIds: [
            "01900000-0000-7000-8000-000000000001",
            "01900000-0000-7000-8000-000000000002"
        ])
        let post = Endpoint.sendPostToFollowers(postId: "post-1", request: request)
        let rss = Endpoint.sendRssFeedItemToFollowers(rssFeedItemId: "item-1", request: request)

        assertEndpoint(
            post,
            method: .POST,
            path: "/api/v1/posts/post-1/sends",
            body: [
                "audience": "selected_followers",
                "recipient_user_ids": ["01900000-0000-7000-8000-000000000001", "01900000-0000-7000-8000-000000000002"]
            ]
        )
        assertEndpoint(
            rss,
            method: .POST,
            path: "/api/v1/rss-feed-items/item-1/sends",
            body: [
                "audience": "selected_followers",
                "recipient_user_ids": ["01900000-0000-7000-8000-000000000001", "01900000-0000-7000-8000-000000000002"]
            ]
        )
    }

    func testSelectedRecipientsRejectEmptyDuplicatesAndMoreThanOneHundred() throws {
        XCTAssertThrowsError(try FollowerDistributionRequest(selectedRecipientIds: [])) {
            XCTAssertEqual($0 as? FollowerDistributionRequestError, .emptySelectedRecipients)
        }
        let recipientId = "01900000-0000-7000-8000-000000000001"
        XCTAssertThrowsError(try FollowerDistributionRequest(selectedRecipientIds: [recipientId, recipientId])) {
            XCTAssertEqual($0 as? FollowerDistributionRequestError, .duplicateSelectedRecipients)
        }
        XCTAssertThrowsError(try FollowerDistributionRequest(selectedRecipientIds: (0 ... 100).map {
            String(format: "01900000-0000-7000-8000-%012d", $0)
        })) {
            XCTAssertEqual($0 as? FollowerDistributionRequestError, .tooManySelectedRecipients)
        }
        XCTAssertThrowsError(try FollowerDistributionRequest(selectedRecipientIds: ["not-a-uuid"])) {
            XCTAssertEqual($0 as? FollowerDistributionRequestError, .invalidRecipientId)
        }
    }

    func testFollowerSearchIncludesQueryAndCursor() {
        let endpoint = Endpoint.userFollowers(userId: "owner / id", query: "alex", after: "cursor", limit: 25)

        XCTAssertEqual(endpoint.path, "/api/v1/users/owner%20%2F%20id/users/followers")
        XCTAssertEqual(endpoint.queryItems, [
            .init(name: "limit", value: "25"),
            .init(name: "q", value: "alex"),
            .init(name: "after", value: "cursor")
        ])
    }

    func testAcceptedResponseDecodesDistributionId() throws {
        let response = try makeVouchaDecoder().decode(
            FollowerDistributionAcceptedResponse.self,
            from: Data(#"{"status":"accepted","distribution_id":"distribution-1"}"#.utf8)
        )

        XCTAssertEqual(response, .init(status: "accepted", distributionId: "distribution-1"))
    }
}

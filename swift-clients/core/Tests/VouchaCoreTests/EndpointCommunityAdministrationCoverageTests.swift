import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class EndpointCommunityAdministrationCoverageTests: XCTestCase {
    func testCommunityMemberManagementEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.updateCommunityMemberRole(idOrSlug: "test community", userId: "user 1", role: .moderator),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/members/user%201",
            body: ["role": "moderator"]
        )
        assertEndpoint(
            Endpoint.removeCommunityMember(idOrSlug: "test community", userId: "user 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/members/user%201"
        )
        assertEndpoint(
            Endpoint.transferCommunityOwnership(idOrSlug: "test community", userId: "user 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/ownership-transfers",
            body: ["user_id": "user 1"]
        )
    }

    func testCommunityListMutationEndpointsUseExpectedRoutesAndBodies() {
        XCTAssertEqual(CommunityListItemType.topic.requestBodyField, "topic_id")
        XCTAssertEqual(CommunityListItemType.rssFeed.requestBodyField, "rss_feed_id")
        XCTAssertEqual(CommunityListItemType.post.requestBodyField, "post_id")
        XCTAssertEqual(CommunityListItemType.urlHostname.requestBodyField, "url_hostname_id")
        XCTAssertEqual(CommunityListItemType.url.requestBodyField, "url_id")
        assertEndpoint(
            Endpoint.addCommunityListTopic(idOrSlug: "test community", topicId: "topic 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/list-items/topics",
            body: ["topic_id": "topic 1"]
        )
        assertEndpoint(
            Endpoint.addCommunityListRssFeed(idOrSlug: "test community", rssFeedId: "feed 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/list-items/rss-feeds",
            body: ["rss_feed_id": "feed 1"]
        )
        assertEndpoint(
            Endpoint.addCommunityListPost(idOrSlug: "test community", postId: "post 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/list-items/posts",
            body: ["post_id": "post 1"]
        )
        assertEndpoint(
            Endpoint.addCommunityListDomain(idOrSlug: "test community", urlHostnameId: "domain 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/list-items/domains",
            body: ["url_hostname_id": "domain 1"]
        )
        assertEndpoint(
            Endpoint.addCommunityListUrl(idOrSlug: "test community", urlId: "url 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/list-items/urls",
            body: ["url_id": "url 1"]
        )
        assertEndpoint(
            Endpoint.removeCommunityListItem(idOrSlug: "test community", itemType: .url, itemId: "item 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/list-items/urls/item%201"
        )
    }
}

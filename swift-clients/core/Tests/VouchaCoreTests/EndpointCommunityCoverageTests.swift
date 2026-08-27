import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointCommunityCoverageTests: XCTestCase {
    func testCommunityEndpointsUseExpectedRoutes() {
        let listing = Endpoint.communities(
            query: "swift",
            after: "cursor 1",
            limit: 12,
            sort: "members",
            memberId: "me",
            listScope: "mine",
            feedCategory: "posts",
            listType: .follow,
            hasListType: true,
            hasListItems: true,
            eligiblePostType: "discussion"
        )
        XCTAssertEqual(listing.path, "/api/v1/communities")
        XCTAssertEqual(listing.queryItems, [
            URLQueryItem(name: "limit", value: "12"),
            URLQueryItem(name: "q", value: "swift"),
            URLQueryItem(name: "after", value: "cursor 1"),
            URLQueryItem(name: "sort", value: "members"),
            URLQueryItem(name: "member_id", value: "me"),
            URLQueryItem(name: "list_scope", value: "mine"),
            URLQueryItem(name: "feed_category", value: "posts"),
            URLQueryItem(name: "list_type", value: "follow"),
            URLQueryItem(name: "has_list_type", value: "true"),
            URLQueryItem(name: "has_list_items", value: "true"),
            URLQueryItem(name: "eligible_post_type", value: "discussion")
        ])

        assertEndpoint(Endpoint.community(idOrSlug: "test community"), path: "/api/v1/communities/test%20community")
        assertEndpoint(
            Endpoint.createCommunity(
                name: "Test Community",
                slug: "test-community",
                markdown: "About",
                visibility: .private,
                listType: .mute,
                memberRosterVisibility: .members,
                memberInvitesAllowedAt: true,
                postApprovalRequiredAt: false,
                allowReviewPosts: true,
                allowDataPointPosts: true,
                turnstileToken: "turnstile-token"
            ),
            method: .POST,
            path: "/api/v1/communities",
            body: [
                "name": "Test Community",
                "slug": "test-community",
                "markdown": "About",
                "visibility": "private",
                "list_type": "mute",
                "member_roster_visibility": "members",
                "member_invites_allowed_at": true,
                "post_approval_required_at": false,
                "allow_review_posts": true,
                "allow_data_point_posts": true,
                "cf_turnstile_response": "turnstile-token"
            ]
        )
        assertEndpoint(
            Endpoint.updateCommunity(
                idOrSlug: "test community",
                memberInvitesAllowedAt: false,
                postApprovalRequiredAt: true
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community",
            body: [
                "member_invites_allowed_at": false,
                "post_approval_required_at": true
            ]
        )
        assertEndpoint(
            Endpoint.updateCommunity(idOrSlug: "test community", archive: true),
            method: .PATCH,
            path: "/api/v1/communities/test%20community",
            body: ["archive": true]
        )
        assertEndpoint(
            Endpoint.updateCommunity(
                idOrSlug: "test community",
                clearMarkdown: true,
                clearListType: true,
                clearProfileImageId: true,
                clearBannerImageId: true,
                clearDefaultLanguage: true
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community",
            body: [
                "markdown": NSNull(),
                "list_type": NSNull(),
                "profile_image_id": NSNull(),
                "banner_image_id": NSNull(),
                "default_language": NSNull()
            ]
        )
        assertEndpoint(
            Endpoint.deleteCommunity(idOrSlug: "test community"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community"
        )
        assertEndpoint(
            Endpoint.joinCommunity(idOrSlug: "test community"),
            method: .POST,
            path: "/api/v1/communities/test%20community/members"
        )
        assertEndpoint(
            Endpoint.leaveCommunity(idOrSlug: "test community"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/members"
        )
        assertEndpoint(
            Endpoint.communityMembers(idOrSlug: "test community", after: "cursor 2", limit: 8, role: .moderator),
            path: "/api/v1/communities/test%20community/members"
        )
        XCTAssertEqual(
            Endpoint.communityMembers(idOrSlug: "test community", after: "cursor 2", limit: 8, role: .moderator)
                .queryItems,
            [
                URLQueryItem(name: "limit", value: "8"),
                URLQueryItem(name: "after", value: "cursor 2"),
                URLQueryItem(name: "role", value: "moderator")
            ]
        )
        assertEndpoint(
            Endpoint.communityPosts(
                idOrSlug: "test community",
                after: "cursor 3",
                limit: 6,
                sort: "hot",
                query: "swift"
            ),
            path: "/api/v1/communities/test%20community/posts"
        )
        XCTAssertEqual(
            Endpoint
                .communityPosts(idOrSlug: "test community", after: "cursor 3", limit: 6, sort: "hot", query: "swift")
                .queryItems,
            [
                URLQueryItem(name: "limit", value: "6"),
                URLQueryItem(name: "after", value: "cursor 3"),
                URLQueryItem(name: "sort", value: "hot"),
                URLQueryItem(name: "q", value: "swift")
            ]
        )
        assertEndpoint(
            Endpoint.communityNews(
                idOrSlug: "test community",
                after: "cursor 4",
                limit: 7,
                feedType: "any",
                query: "swift",
                hasRelatedPosts: true
            ),
            path: "/api/v1/communities/test%20community/news"
        )
        XCTAssertEqual(
            Endpoint.communityNews(
                idOrSlug: "test community",
                after: "cursor 4",
                limit: 7,
                feedType: "any",
                query: "swift",
                hasRelatedPosts: true
            ).queryItems,
            [
                URLQueryItem(name: "limit", value: "7"),
                URLQueryItem(name: "after", value: "cursor 4"),
                URLQueryItem(name: "feed_type", value: "any"),
                URLQueryItem(name: "q", value: "swift"),
                URLQueryItem(name: "has_related_posts", value: "true")
            ]
        )
        assertEndpoint(
            Endpoint.communityListItemCounts(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/list-items/counts"
        )
        assertEndpoint(
            Endpoint.communityListItems(idOrSlug: "test community", itemType: .topic, after: "cursor 5", limit: 9),
            path: "/api/v1/communities/test%20community/list-items/topics"
        )
        XCTAssertEqual(
            Endpoint.communityListItems(idOrSlug: "test community", itemType: .topic, after: "cursor 5", limit: 9)
                .queryItems,
            [
                URLQueryItem(name: "limit", value: "9"),
                URLQueryItem(name: "after", value: "cursor 5")
            ]
        )
        assertEndpoint(
            Endpoint.communityListItems(idOrSlug: "test community", itemType: .rssFeed),
            path: "/api/v1/communities/test%20community/list-items/rss-feeds"
        )
        assertEndpoint(
            Endpoint.communityListItems(idOrSlug: "test community", itemType: .post),
            path: "/api/v1/communities/test%20community/list-items/posts"
        )
        assertEndpoint(
            Endpoint.communityListItems(idOrSlug: "test community", itemType: .urlHostname),
            path: "/api/v1/communities/test%20community/list-items/domains"
        )
        assertEndpoint(
            Endpoint.communityListItems(idOrSlug: "test community", itemType: .url),
            path: "/api/v1/communities/test%20community/list-items/urls"
        )
        assertEndpoint(
            Endpoint.communityApplicationQuestions(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/application-questions"
        )
        assertEndpoint(
            Endpoint.setCommunityApplicationQuestions(
                idOrSlug: "test community",
                questions: [CommunityApplicationQuestionInput(question: "Why?", fieldType: .shortText)]
            ),
            method: .PUT,
            path: "/api/v1/communities/test%20community/application-questions",
            body: ["questions": [["question": "Why?", "field_type": "short_text"]]]
        )
        assertEndpoint(
            Endpoint.submitCommunityApplication(
                idOrSlug: "test community",
                answers: ["why": .string("Because")],
                message: "Hello"
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/applications",
            body: ["answers": ["why": "Because"], "message": "Hello"]
        )
        assertEndpoint(
            Endpoint.communityPinnedPosts(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/pinned-posts"
        )
        assertEndpoint(
            Endpoint.updateCommunityPinnedPosts(idOrSlug: "test community", postIds: ["post-1", "post-2"]),
            method: .PUT,
            path: "/api/v1/communities/test%20community/pinned-posts",
            body: ["post_ids": ["post-1", "post-2"]]
        )
        assertEndpoint(
            Endpoint.communityPendingPosts(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/posts/pending"
        )
        assertEndpoint(
            Endpoint.communityPendingReports(idOrSlug: "test community", sort: "severity"),
            path: "/api/v1/communities/test%20community/reports/pending"
        )
        XCTAssertEqual(
            Endpoint.communityPendingReports(idOrSlug: "test community", sort: "severity").queryItems,
            [
                URLQueryItem(name: "limit", value: "50"),
                URLQueryItem(name: "sort", value: "severity")
            ]
        )
        assertEndpoint(
            Endpoint.archiveCommunity(idOrSlug: "test community"),
            method: .PATCH,
            path: "/api/v1/communities/test%20community",
            body: ["archive": true]
        )
        assertEndpoint(
            Endpoint.unarchiveCommunity(idOrSlug: "test community"),
            method: .PATCH,
            path: "/api/v1/communities/test%20community",
            body: ["archive": false]
        )
    }
}

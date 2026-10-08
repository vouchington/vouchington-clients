import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointCommunityExtrasCoverageTests: XCTestCase {
    func testCommunitySavedRepliesAutomodWarningAndPostTypeSettingsEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.communitySavedReplies(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/saved-replies"
        )
        assertEndpoint(
            Endpoint.createCommunitySavedReply(
                idOrSlug: "test community",
                title: "Greeting",
                body: "Thanks for writing in."
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/saved-replies",
            body: ["title": "Greeting", "body": "Thanks for writing in."]
        )
        let recentActions = Endpoint.communityAutomodRecentActions(
            idOrSlug: "test community",
            limit: 25,
            window: "24h",
            source: "agent_moderation"
        )
        assertEndpoint(recentActions, path: "/api/v1/communities/test%20community/automod/recent-actions")
        XCTAssertEqual(recentActions.queryItems, [
            URLQueryItem(name: "limit", value: "25"),
            URLQueryItem(name: "window", value: "24h"),
            URLQueryItem(name: "source", value: "agent_moderation")
        ])
        assertEndpoint(
            Endpoint.recordCommunityAutomodFeedback(
                idOrSlug: "test community",
                sourceKey: "agent_moderation:post-1",
                outcome: .truePositive,
                action: .keepRemoved,
                reasonCode: "correct",
                note: "Matches the community rules."
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/automod/recent-actions/agent_moderation%3Apost-1/feedback",
            body: [
                "outcome": "true_positive",
                "action": "keep_removed",
                "reason_code": "correct",
                "note": "Matches the community rules."
            ]
        )
        assertEndpoint(
            Endpoint.communityModerationResults(idOrSlug: "test community", postId: "post 1"),
            path: "/api/v1/communities/test%20community/posts/post%201/moderation-results"
        )
        assertEndpoint(
            Endpoint.createCommunityWarning(
                idOrSlug: "test community",
                userId: "user 1",
                reason: "Spam in community",
                publicMessage: "Please read the community rules.",
                reportId: "report 1",
                resolveReport: false
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/warnings",
            body: [
                "userId": "user 1",
                "reason": "Spam in community",
                "publicMessage": "Please read the community rules.",
                "reportId": "report 1",
                "resolveReport": false
            ]
        )
        assertEndpoint(
            Endpoint.updateCommunityPostTypeSettings(
                idOrSlug: "test community",
                shouldAllowReviewPosts: true,
                shouldAllowDataPointPosts: true
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/post-type-settings",
            body: [
                "should_allow_review_posts": true,
                "should_allow_data_point_posts": true
            ]
        )
    }
}

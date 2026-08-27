import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class EndpointCommunityModerationCoverageTests: XCTestCase {
    func testCommunityPostReviewBanRestrictionAndStatsEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.approveCommunityPostReview(idOrSlug: "test community", postId: "post 1"),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/posts/post%201",
            body: ["status": "approved"]
        )
        assertEndpoint(
            Endpoint.rejectCommunityPostReview(
                idOrSlug: "test community",
                postId: "post 1",
                rejectionReason: "Needs sources"
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/posts/post%201",
            body: ["status": "rejected", "reason": "Needs sources"]
        )
        assertEndpoint(
            Endpoint.unpublishCommunityPostReview(idOrSlug: "test community", postId: "post 1"),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/posts/post%201",
            body: ["status": "unpublished"]
        )
        assertEndpoint(
            Endpoint.banCommunityMember(idOrSlug: "test community", userId: "user 1", reason: "spam"),
            method: .POST,
            path: "/api/v1/communities/test%20community/bans",
            body: ["user_id": "user 1", "reason": "spam"]
        )
        assertEndpoint(
            Endpoint.liftCommunityBan(idOrSlug: "test community", userId: "user 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/bans/user%201"
        )
        assertEndpoint(
            Endpoint.activateCommunityRestrictions(
                idOrSlug: "test community",
                restrictionTypes: [.requirePostApproval, .noNewMemberPosts],
                reason: "cooldown"
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/restrictions",
            body: ["restriction_types": ["require_post_approval", "no_new_member_posts"], "reason": "cooldown"]
        )
        assertEndpoint(
            Endpoint.liftCommunityRestriction(idOrSlug: "test community", restrictionId: "restriction 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/restrictions/restriction%201"
        )
        let stats = Endpoint.communityModeratorStats(idOrSlug: "test community", window: 14)
        assertEndpoint(stats, path: "/api/v1/communities/test%20community/moderator-stats")
        XCTAssertEqual(stats.queryItems, [URLQueryItem(name: "window", value: "14")])
    }

    func testCommunityModmailAndQueueEndpointsUseExpectedRoutesAndBodies() {
        let modlog = Endpoint.communityModlog(idOrSlug: "test community", after: "cursor 1", actionType: "ban")
        assertEndpoint(modlog, path: "/api/v1/communities/test%20community/modlog")
        XCTAssertEqual(modlog.queryItems, [
            URLQueryItem(name: "after", value: "cursor 1"),
            URLQueryItem(name: "action_type", value: "ban")
        ])
        let modmail = Endpoint.communityModmail(idOrSlug: "test community", after: "cursor 2", limit: 11)
        assertEndpoint(modmail, path: "/api/v1/communities/test%20community/modmail")
        XCTAssertEqual(modmail.queryItems, [
            URLQueryItem(name: "after", value: "cursor 2"),
            URLQueryItem(name: "limit", value: "11")
        ])
        assertEndpoint(
            Endpoint.openCommunityModmailThread(idOrSlug: "test community", subjectUserId: "user 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/modmail",
            body: ["subject_user_id": "user 1"]
        )
        assertEndpoint(
            Endpoint.openCommunityModmailThreadForReport(idOrSlug: "test community", reportId: "report 1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/reports/report%201/modmail",
            body: [:]
        )
        let messages = Endpoint.communityModmailMessages(
            idOrSlug: "test community",
            conversationId: "thread 1",
            after: "cursor 3",
            limit: 12
        )
        assertEndpoint(messages, path: "/api/v1/communities/test%20community/modmail/thread%201/messages")
        XCTAssertEqual(messages.queryItems, [
            URLQueryItem(name: "after", value: "cursor 3"),
            URLQueryItem(name: "limit", value: "12")
        ])
        assertEndpoint(
            Endpoint.sendCommunityModmailMessage(idOrSlug: "test community", conversationId: "thread 1", text: "Hello"),
            method: .POST,
            path: "/api/v1/communities/test%20community/modmail/thread%201/messages",
            body: ["text": "Hello"]
        )
        assertEndpoint(
            Endpoint.updateCommunityModmailThread(
                idOrSlug: "test community",
                conversationId: "thread 1",
                assignedModId: "mod 1",
                resolved: true
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/modmail/thread%201",
            body: ["assigned_mod_id": "mod 1", "resolved": true]
        )
        let queue = Endpoint.communityModerationQueue(idOrSlug: "test community", after: "cursor 4", limit: 13)
        assertEndpoint(queue, path: "/api/v1/communities/test%20community/moderation-queue")
        XCTAssertEqual(queue.queryItems, [
            URLQueryItem(name: "after", value: "cursor 4"),
            URLQueryItem(name: "limit", value: "13")
        ])
    }

    func testCommunitySavedRepliesClaimAutomodAnalyticsAndVacationEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.communitySavedReplies(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/saved-replies"
        )
        assertEndpoint(
            Endpoint.createCommunitySavedReply(idOrSlug: "test community", title: "Greeting", body: "Hello"),
            method: .POST,
            path: "/api/v1/communities/test%20community/saved-replies",
            body: ["title": "Greeting", "body": "Hello"]
        )
        assertEndpoint(
            Endpoint.deleteCommunitySavedReply(idOrSlug: "test community", replyId: "reply 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/saved-replies/reply%201"
        )
        assertEndpoint(
            Endpoint.claimCommunityModerationReport(idOrSlug: "test community", reportId: "report 1"),
            method: .PUT,
            path: "/api/v1/communities/test%20community/reports/report%201/claim",
            body: [:]
        )
        assertEndpoint(
            Endpoint.releaseCommunityModerationReport(idOrSlug: "test community", reportId: "report 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/reports/report%201/claim"
        )
        assertEndpoint(
            Endpoint.claimCommunityPendingPost(idOrSlug: "test community", postId: "post 1"),
            method: .PUT,
            path: "/api/v1/communities/test%20community/posts/post%201/claim",
            body: [:]
        )
        assertEndpoint(
            Endpoint.releaseCommunityPendingPost(idOrSlug: "test community", postId: "post 1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/posts/post%201/claim"
        )
        let actions = Endpoint.communityAutomodRecentActions(
            idOrSlug: "test community",
            after: "cursor 5",
            limit: 14,
            window: "7d",
            source: "queue",
            agent: "agent",
            postType: "discussion",
            maxConfidence: 0.75
        )
        assertEndpoint(actions, path: "/api/v1/communities/test%20community/automod/recent-actions")
        XCTAssertEqual(actions.queryItems, [
            URLQueryItem(name: "after", value: "cursor 5"),
            URLQueryItem(name: "limit", value: "14"),
            URLQueryItem(name: "window", value: "7d"),
            URLQueryItem(name: "source", value: "queue"),
            URLQueryItem(name: "agent", value: "agent"),
            URLQueryItem(name: "post_type", value: "discussion"),
            URLQueryItem(name: "max_confidence", value: "0.75")
        ])
        assertEndpoint(
            Endpoint.recordCommunityAutomodFeedback(
                idOrSlug: "test community",
                sourceKey: "source 1",
                outcome: .falsePositive,
                action: .keepRemoved,
                reasonCode: "ok",
                note: "Reviewed"
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/automod/recent-actions/source%201/feedback",
            body: ["outcome": "false_positive", "action": "keep_removed", "reason_code": "ok", "note": "Reviewed"]
        )
        let analytics = Endpoint.communityModerationAnalytics(idOrSlug: "test community", range: "30d")
        assertEndpoint(analytics, path: "/api/v1/communities/test%20community/moderation-analytics")
        XCTAssertEqual(analytics.queryItems, [URLQueryItem(name: "range", value: "30d")])
        let transparency = Endpoint.communityModerationTransparency(
            idOrSlug: "test community",
            range: "all",
            after: "older-page"
        )
        assertEndpoint(
            transparency,
            path: "/api/v1/communities/test%20community/moderation-transparency"
        )
        XCTAssertEqual(transparency.queryItems, [
            URLQueryItem(name: "range", value: "all"),
            URLQueryItem(name: "after", value: "older-page")
        ])
        assertEndpoint(
            Endpoint.communityModeratorVacation(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/moderator-vacation"
        )
        assertEndpoint(
            Endpoint.setCommunityModeratorVacation(idOrSlug: "test community", endsAt: nil),
            method: .PUT,
            path: "/api/v1/communities/test%20community/moderator-vacation",
            body: [:]
        )
        assertEndpoint(
            Endpoint.clearCommunityModeratorVacation(idOrSlug: "test community"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/moderator-vacation"
        )
        assertEndpoint(
            Endpoint.setSuppressCommunityDigestsWhileOnVacation(
                idOrSlug: "test community",
                suppress: true
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/moderator-vacation",
            body: ["suppress_community_digests_while_on_vacation": true]
        )
    }
}

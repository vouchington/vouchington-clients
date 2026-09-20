import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaModels
import XCTest

final class UserFacingEndpointCoverageTests: XCTestCase {
    override func tearDown() {
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.lastRequestURL = nil
        super.tearDown()
    }

    func testCombinedSearchEncodesQueryAndLimit() async throws {
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [CapturingURLProtocol.self]
        )

        struct EmptyResponse: Decodable {}
        let _: EmptyResponse = try await client.send(Endpoint.combinedSearch(query: "cats & dogs #1", limit: 7))

        let url = try XCTUnwrap(CapturingURLProtocol.lastRequestURL)
        XCTAssertEqual(url.path, "/api/v1/search")
        XCTAssertTrue(url.absoluteString.contains("cats%20%26%20dogs%20%231"))
        XCTAssertTrue(url.absoluteString.contains("limit=7"))

        let components = try XCTUnwrap(URLComponents(url: url, resolvingAgainstBaseURL: false))
        XCTAssertEqual(components.queryItems?.first(where: { $0.name == "q" })?.value, "cats & dogs #1")
    }

    func testDiscoveryAndAccountEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.webSearch(query: "swift", limit: 11), path: "/api/v1/web-search")

        let trendingCommunities = Endpoint.trendingCommunities(after: "cursor-1", limit: 8)
        XCTAssertEqual(trendingCommunities.path, "/api/v1/trending-communities")
        XCTAssertEqual(trendingCommunities.queryItems, [
            URLQueryItem(name: "limit", value: "8"),
            URLQueryItem(name: "after", value: "cursor-1")
        ])

        let trendingReferralPrograms = Endpoint.trendingReferralPrograms(after: "cursor-2", limit: 4)
        XCTAssertEqual(trendingReferralPrograms.path, "/api/v1/trending-referral-programs")
        XCTAssertEqual(trendingReferralPrograms.queryItems, [
            URLQueryItem(name: "limit", value: "4"),
            URLQueryItem(name: "after", value: "cursor-2")
        ])
        assertEndpoint(
            Endpoint.referralLinksFeed(feedType: "mutual_follows", after: "cursor-2", limit: 4),
            path: "/api/v1/feeds/referral_links/mutual_follows"
        )

        let recommendedTopics = Endpoint.recommendedTopics(
            after: "cursor-3",
            limit: 6,
            topicTypes: ["card", "topic"],
            sort: .best,
            spendingCategory: true,
            rssFeed: false
        )
        XCTAssertEqual(recommendedTopics.path, "/api/v1/recommended-topics")
        XCTAssertEqual(recommendedTopics.queryItems, [
            URLQueryItem(name: "limit", value: "6"),
            URLQueryItem(name: "after", value: "cursor-3"),
            URLQueryItem(name: "topic_types", value: "card,topic"),
            URLQueryItem(name: "sort", value: "best"),
            URLQueryItem(name: "spending_category", value: "true"),
            URLQueryItem(name: "rss_feed", value: "false")
        ])
        assertEndpoint(
            Endpoint.topicRecommendations(after: "cursor-3", limit: 6),
            path: "/api/v1/topic-recommendations"
        )
        assertEndpoint(
            Endpoint.topicRecommendation(id: "recommendation 1"),
            path: "/api/v1/topic-recommendations/recommendation%201"
        )

        assertEndpoint(Endpoint.myCommunities, path: "/api/v1/my/communities")
        assertEndpoint(Endpoint.myReferralClicks(after: "cursor-4", limit: 12), path: "/api/v1/my/referral-clicks")
        assertEndpoint(Endpoint.referralLinks(after: "cursor-4", limit: 12), path: "/api/v1/referral-links")
        assertEndpoint(Endpoint.publisherTypes(), path: "/api/v1/topics/publisher-types")
        let topicsEndpoint = Endpoint.topics(
            query: "swift",
            limit: 5,
            topicTypes: ["topic"],
            sort: "relevance",
            spendingCategory: true,
            rssFeed: false
        )
        assertEndpoint(topicsEndpoint, path: "/api/v1/topics")
        XCTAssertEqual(topicsEndpoint.queryItems, [
            URLQueryItem(name: "limit", value: "5"),
            URLQueryItem(name: "q", value: "swift"),
            URLQueryItem(name: "topic_types", value: "topic"),
            URLQueryItem(name: "sort", value: "relevance"),
            URLQueryItem(name: "spending_category", value: "true"),
            URLQueryItem(name: "rss_feed", value: "false")
        ])
        let postsEndpoint = Endpoint.posts(query: "swift", limit: 5)
        assertEndpoint(postsEndpoint, path: "/api/v1/posts")
        XCTAssertEqual(postsEndpoint.queryItems, [
            URLQueryItem(name: "limit", value: "5"),
            URLQueryItem(name: "sort", value: "relevance"),
            URLQueryItem(name: "q", value: "swift")
        ])
        let urlsEndpoint = Endpoint.urls(query: "swift", limit: 5)
        assertEndpoint(urlsEndpoint, path: "/api/v1/urls")
        XCTAssertEqual(urlsEndpoint.queryItems, [
            URLQueryItem(name: "limit", value: "5"),
            URLQueryItem(name: "query", value: "swift")
        ])
        assertEndpoint(
            Endpoint.createReferralLink(
                referralProgramId: "program-1",
                url: "https://example.com/apply",
                label: "Apply"
            ),
            method: .POST,
            path: "/api/v1/referral-links",
            body: [
                "referral_program_id": "program-1",
                "url": "https://example.com/apply",
                "label": "Apply"
            ]
        )
        assertEndpoint(
            Endpoint.updateReferralLink(id: "link 1", label: "Apply"),
            method: .PATCH,
            path: "/api/v1/referral-links/link%201",
            body: ["label": "Apply"]
        )
        assertEndpoint(
            Endpoint.updateReferralLink(id: "link 1", label: nil),
            method: .PATCH,
            path: "/api/v1/referral-links/link%201",
            body: ["label": NSNull()]
        )
        assertEndpoint(
            Endpoint.deleteReferralLink(id: "link 1"),
            method: .DELETE,
            path: "/api/v1/referral-links/link%201"
        )
        assertEndpoint(
            Endpoint.activateReferralLink(id: "link 1"),
            method: .POST,
            path: "/api/v1/referral-links/link%201/activations",
            body: [:]
        )
        assertEndpoint(
            Endpoint.deactivateReferralLink(id: "link 1"),
            method: .DELETE,
            path: "/api/v1/referral-links/link%201/activations"
        )
        let prioritizedReferralLinks = Endpoint.prioritizedReferralLinks(referralProgramId: "program 1", all: true)
        XCTAssertEqual(prioritizedReferralLinks.path, "/api/v1/topics/program%201/prioritized-referral-links")
        XCTAssertEqual(prioritizedReferralLinks.queryItems, [URLQueryItem(name: "all", value: "true")])
        let referralProgramSearch = Endpoint.topics(
            query: "test",
            topicTypes: ["referral_program"],
            limit: 10
        )
        XCTAssertEqual(referralProgramSearch.path, "/api/v1/topics")
        XCTAssertEqual(referralProgramSearch.queryItems, [
            URLQueryItem(name: "q", value: "test"),
            URLQueryItem(name: "topic_types", value: "referral_program"),
            URLQueryItem(name: "limit", value: "10")
        ])
        assertEndpoint(
            Endpoint.referralProgramValidationInfo(topicId: "program 1"),
            path: "/api/v1/topics/program%201/referral-program/validation-info"
        )
        assertEndpoint(Endpoint.membershipPlans, path: "/api/v1/memberships/plans")
        assertEndpoint(
            Endpoint.grantMembership(userId: "user-1", plan: .pro, skuId: "sku-1"),
            method: .POST,
            path: "/api/v1/membership-grants",
            body: ["user_id": "user-1", "plan": "pro", "sku_id": "sku-1", "duration_days": 30]
        )
        assertEndpoint(
            Endpoint.bookmarks(entityType: "topic", entityId: "topic 1"),
            path: "/api/v1/bookmarks/topic/topic%201"
        )
        assertEndpoint(
            Endpoint.bookmark(entityType: "topic", entityId: "topic 1", predicate: "follow"),
            method: .PUT,
            path: "/api/v1/bookmarks/topic/topic%201/follow"
        )
        assertEndpoint(
            Endpoint.unbookmark(entityType: "topic", entityId: "topic 1", predicate: "follow"),
            method: .DELETE,
            path: "/api/v1/bookmarks/topic/topic%201/follow"
        )
        assertEndpoint(
            Endpoint.entityRelations(
                entityType: "topic",
                entityId: "topic 1",
                predicate: "category",
                objectType: "topic",
                sort: "best",
                positiveNetVoteScore: true
            ),
            path: "/api/v1/entity-relations/topic/topic%201/category/topic"
        )
        assertEndpoint(
            Endpoint.createEntityRelation(
                entityType: "topic",
                entityId: "topic 1",
                predicate: "category",
                objectType: "topic",
                objectId: "topic-2"
            ),
            method: .POST,
            path: "/api/v1/entity-relations/topic/topic%201/category/topic",
            body: ["objectId": "topic-2"]
        )
        assertEndpoint(
            Endpoint.voteEntityRelation(relationId: "relation 1", choice: .confirm),
            method: .PUT,
            path: "/api/v1/entity-relations/relation%201/vote",
            body: ["choice": "confirm"]
        )
        let filteredRelations = Endpoint.entityRelations(
            entityType: "topic",
            entityId: "topic 1",
            predicate: "category",
            objectType: "topic",
            sort: "best",
            positiveNetVoteScore: false
        )
        XCTAssertEqual(filteredRelations.path, "/api/v1/entity-relations/topic/topic%201/category/topic")
        XCTAssertEqual(filteredRelations.queryItems, [
            URLQueryItem(name: "sort", value: "best"),
            URLQueryItem(name: "positiveNetVoteScore", value: "false")
        ])
    }

    func testCreatePostEndpointUsesBackendContract() {
        assertEndpoint(
            Endpoint.createPost(
                postType: .discussion,
                title: "Native title",
                markdown: "Native body",
                turnstileToken: "turnstile-token",
                recaptchaToken: "recaptcha-token",
                idempotencyKey: "00000000-0000-4000-8000-000000000010"
            ),
            method: .POST,
            path: "/api/v1/posts",
            body: [
                "post_type": "discussion",
                "title": "Native title",
                "markdown": "Native body",
                "broadcast": "everyone",
                "privacy": "public",
                "is_anonymous": false,
                "cf_turnstile_response": "turnstile-token",
                "recaptcha_token": "recaptcha-token"
            ]
        )

        assertEndpoint(
            Endpoint.createPost(
                postType: .link,
                title: "Native link",
                markdown: "",
                url: "https://example.com/native",
                idempotencyKey: "00000000-0000-4000-8000-000000000011"
            ),
            method: .POST,
            path: "/api/v1/posts",
            body: [
                "post_type": "link",
                "title": "Native link",
                "markdown": "",
                "broadcast": "everyone",
                "privacy": "public",
                "is_anonymous": false,
                "url": "https://example.com/native"
            ]
        )

        assertEndpoint(
            Endpoint.createPost(
                postType: .review,
                title: "Native review",
                markdown: "Review body",
                reviewTopicRatings: [
                    .init(topicId: "topic-1", rating: 5)
                ],
                turnstileToken: "turnstile-token",
                idempotencyKey: "00000000-0000-4000-8000-000000000012"
            ),
            method: .POST,
            path: "/api/v1/posts",
            body: [
                "post_type": "review",
                "title": "Native review",
                "markdown": "Review body",
                "broadcast": "everyone",
                "privacy": "public",
                "is_anonymous": false,
                "review_topic_ratings": [["topic_id": "topic-1", "rating": 5]],
                "cf_turnstile_response": "turnstile-token"
            ]
        )

        assertEndpoint(
            Endpoint.createPost(
                postType: .dataPoint,
                title: "Native data point",
                markdown: "Data point body",
                dataPointVertical: .creditCard,
                structuredData: .object([
                    "vertical": .string("credit_card"),
                    "schema_version": .integer(1),
                    "topic_ids": .array([.string("topic-1")]),
                    "result": .string("approved")
                ]),
                turnstileToken: "turnstile-token",
                idempotencyKey: "00000000-0000-4000-8000-000000000013"
            ),
            method: .POST,
            path: "/api/v1/posts",
            body: [
                "post_type": "data_point",
                "title": "Native data point",
                "markdown": "Data point body",
                "broadcast": "everyone",
                "privacy": "public",
                "is_anonymous": false,
                "data_point_vertical": "credit_card",
                "structured_data": [
                    "vertical": "credit_card",
                    "schema_version": 1,
                    "topic_ids": ["topic-1"],
                    "result": "approved"
                ],
                "cf_turnstile_response": "turnstile-token"
            ]
        )
    }

    func testConversationAndAppealEndpointsUseExpectedRoutes() {
        let myConversations = Endpoint.myConversations(after: "cursor-5", limit: 19)
        XCTAssertEqual(myConversations.path, "/api/v1/my/conversations")
        XCTAssertEqual(myConversations.queryItems, [
            URLQueryItem(name: "limit", value: "19"),
            URLQueryItem(name: "after", value: "cursor-5")
        ])

        assertEndpoint(
            Endpoint.createConversation(title: "Test chat"),
            method: .POST,
            path: "/api/v1/conversations",
            body: ["title": "Test chat"]
        )

        let messageThread = Endpoint.myConversationMessages(conversationId: "conv-1", after: "msg-1", limit: 9)
        XCTAssertEqual(messageThread.path, "/api/v1/my/conversations/conv-1/messages")
        XCTAssertEqual(messageThread.queryItems, [
            URLQueryItem(name: "limit", value: "9"),
            URLQueryItem(name: "after", value: "msg-1")
        ])
        assertEndpoint(
            Endpoint.myConversationTitle(conversationId: "conv-1"),
            method: .POST,
            path: "/api/v1/my/conversations/conv-1/title"
        )
        assertEndpoint(
            Endpoint.renameConversation(conversationId: "conv-1", title: "Renamed"),
            method: .PATCH,
            path: "/api/v1/my/conversations/conv-1",
            body: ["title": "Renamed"]
        )
        assertEndpoint(
            Endpoint.deleteConversation(conversationId: "conv-1"),
            method: .DELETE,
            path: "/api/v1/my/conversations/conv-1"
        )
        let streamEndpoint = Endpoint.chatConversationStream(conversationId: "conv-1", message: "Hello")
        assertEndpoint(
            streamEndpoint,
            method: .POST,
            path: "/api/v1/conversations/conv-1/chat",
            body: ["message": "Hello"]
        )
        XCTAssertEqual(streamEndpoint.headers["Accept"], "text/event-stream")

        assertEndpoint(
            Endpoint.myConversationParticipants(conversationId: "conv-1"),
            path: "/api/v1/my/messages/conv-1/participants"
        )
        assertEndpoint(Endpoint.myMessages(after: "cursor-6", limit: 17), path: "/api/v1/my/messages")
        assertEndpoint(
            Endpoint.myMessageConversationMessages(conversationId: "conv-2", after: "msg-2", limit: 7),
            path: "/api/v1/my/messages/conv-2/messages"
        )
        assertEndpoint(
            Endpoint.myMessageConversationParticipants(conversationId: "conv-2"),
            path: "/api/v1/my/messages/conv-2/participants"
        )

        let supportThreads = Endpoint.mySupportThreads(after: "cursor-9", limit: 13)
        XCTAssertEqual(supportThreads.path, "/api/v1/my/support-threads")
        XCTAssertEqual(supportThreads.queryItems, [
            URLQueryItem(name: "limit", value: "13"),
            URLQueryItem(name: "after", value: "cursor-9")
        ])
        let supportThread = Endpoint.mySupportThread(threadId: "thread-1", after: "cursor-3", limit: 9)
        XCTAssertEqual(supportThread.path, "/api/v1/my/support-threads/thread-1")
        XCTAssertEqual(supportThread.queryItems, [
            URLQueryItem(name: "limit", value: "9"),
            URLQueryItem(name: "after", value: "cursor-3")
        ])
        assertEndpoint(
            Endpoint.createSupportThread(subject: "Need help", message: "Hello", conversationId: "conv-1"),
            method: .POST,
            path: "/api/v1/my/support-threads",
            body: [
                "subject": "Need help",
                "message": "Hello",
                "conversation_id": "conv-1"
            ]
        )

        let appeals = Endpoint.appeals(status: .pending, limit: 5, after: "cursor-7", mine: true)
        XCTAssertEqual(appeals.path, "/api/v1/appeals")
        XCTAssertEqual(appeals.queryItems, [
            URLQueryItem(name: "limit", value: "5"),
            URLQueryItem(name: "status", value: "pending"),
            URLQueryItem(name: "after", value: "cursor-7"),
            URLQueryItem(name: "mine", value: "true")
        ])
        let disputes = Endpoint.disputes(status: .pending, limit: 5, after: "cursor-8", mine: true)
        XCTAssertEqual(disputes.path, "/api/v1/disputes")
        XCTAssertEqual(disputes.queryItems, [
            URLQueryItem(name: "limit", value: "5"),
            URLQueryItem(name: "status", value: "pending"),
            URLQueryItem(name: "after", value: "cursor-8"),
            URLQueryItem(name: "mine", value: "true")
        ])

        assertEndpoint(Endpoint.appeal(id: "appeal-1"), path: "/api/v1/appeals/appeal-1")
        assertEndpoint(
            Endpoint.submitAppeal(
                targetType: .removal,
                targetId: "target-1",
                appealReason: "Please review this again.",
                postRemovalKind: .community,
                turnstileToken: "turnstile-token"
            ),
            method: .POST,
            path: "/api/v1/appeals",
            body: [
                "target_type": "removal",
                "target_id": "target-1",
                "appeal_reason": "Please review this again.",
                "post_removal_kind": "community",
                "cf_turnstile_response": "turnstile-token"
            ]
        )
        assertEndpoint(
            Endpoint.updateAppeal(id: "appeal-1", publicResponse: "done", internalNotes: "note"),
            method: .PATCH,
            path: "/api/v1/appeals/appeal-1",
            body: ["public_response": "done", "internal_notes": "note"]
        )
        assertEndpoint(
            Endpoint.appealApproval(id: "appeal-1"),
            method: .POST,
            path: "/api/v1/appeals/appeal-1/approval"
        )
        assertEndpoint(
            Endpoint.appealDelivery(id: "appeal-1"),
            method: .POST,
            path: "/api/v1/appeals/appeal-1/delivery"
        )
        assertEndpoint(
            Endpoint.appealResolution(id: "appeal-1", action: .accept),
            method: .POST,
            path: "/api/v1/appeals/appeal-1/resolution",
            body: ["action": "accept"]
        )
        assertEndpoint(
            Endpoint.appealResolutionDrafts(id: "appeal-1"),
            method: .POST,
            path: "/api/v1/appeals/appeal-1/resolution-drafts"
        )
    }
}

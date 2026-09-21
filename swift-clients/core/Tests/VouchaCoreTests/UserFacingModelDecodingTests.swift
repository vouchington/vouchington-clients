import Foundation
@testable import VouchaModels
import XCTest

final class UserFacingModelDecodingTests: XCTestCase {
    func testDecodesDiscoveryAndAccountModels() throws {
        let decoder = makeVouchaDecoder()

        XCTAssertEqual(RecommendedTopicsSort.score.rawValue, "score")
        XCTAssertEqual(RecommendedTopicsSort.best.rawValue, "best")

        let webSearchJSON = Data(
            #"{"results":[{"url":{"id":"url-1","hostname":{"id":"host-1","hostname":"example.com","topic_id":"topic-1"},"canonical_url_id":null,"url":"https://example.com/article","pathname":"/article","search_params":{"q":"swift"}},"snippet":"Swift search result","match_type":"content"}],"page_info":{"has_next_page":true,"start_cursor":"cursor-a","end_cursor":"cursor-b"}}"#
                .utf8
        )
        let webSearch = try decoder.decode(WebSearchResponse.self, from: webSearchJSON)
        XCTAssertEqual(webSearch.results.first?.id, "url-1")
        XCTAssertEqual(webSearch.results.first?.matchType, "content")
        XCTAssertEqual(webSearch.results.first?.url.hostname.hostname, "example.com")
        XCTAssertEqual(webSearch.pageInfo.endCursor, "cursor-b")

        let fediverseSearchJSON = Data(
            #"{"buckets":[{"provider":"peertube","status":"ok","items":[{"provider":"peertube","result_type":"video","external_url":"https://video.example.test/w/1","title":"Swift video","summary":"A PeerTube result","author_name":"Alice","author_url":"https://video.example.test/a/alice","published_at":"2026-07-01T12:00:00Z","thumbnail_url":"https://video.example.test/thumb.jpg","source_hostname":"video.example.test"}],"next_cursor":"cursor-c"}]}"#
                .utf8
        )
        let fediverseSearch = try decoder.decode(FediverseSearchResponse.self, from: fediverseSearchJSON)
        XCTAssertEqual(fediverseSearch.buckets.first?.provider, "peertube")
        XCTAssertEqual(fediverseSearch.buckets.first?.items.first?.title, "Swift video")
        XCTAssertEqual(fediverseSearch.buckets.first?.items.first?.id, "https://video.example.test/w/1")

        let communitiesJSON = Data(
            #"{"communities":[{"id":"community-1","trending_score":12.5,"member_count":8,"post_count":2,"virtual_subscription_count":3}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                .utf8
        )
        let communities = try decoder.decode(TrendingCommunitiesResponse.self, from: communitiesJSON)
        XCTAssertEqual(communities.communities.first?.memberCount, 8)
        XCTAssertEqual(communities.communities.first?.trendingScore, 12.5)

        let referralProgramsJSON = Data(
            #"{"referral_programs":[{"id":"program-1","trending_score":9.0,"link_count":4}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                .utf8
        )
        let referralPrograms = try decoder.decode(TrendingReferralProgramsResponse.self, from: referralProgramsJSON)
        XCTAssertEqual(referralPrograms.referralPrograms.first?.linkCount, 4)

        let recommendedJSON = Data(
            #"{"results":[{"id":"topic-1","score":4.2,"reason":"from_viewed_posts"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},"topics":{},"topics_metrics":{},"bookmarks":{}}"#
                .utf8
        )
        let recommended = try decoder.decode(RecommendedTopicsResponse.self, from: recommendedJSON)
        XCTAssertEqual(recommended.results.first?.reason, "from_viewed_posts")

        let referralClicksJSON = Data(
            #"{"results":[{"id":"click-1"}],"clicks":{"click-1":{"id":"click-1","landing_url":"https://voucha.ai/@alice","signed_up_at":null,"user_id":"user-1","created_at":"2026-03-01T11:55:00Z"}},"users":{"user-1":{"id":"user-1","username":"alice","roles":[],"profile_image_id":null,"markdown":null}},"page_info":{"has_next_page":false,"start_cursor":"click-1","end_cursor":null}}"#
                .utf8
        )
        let referralClicks = try decoder.decode(ReferralClickLogResponse.self, from: referralClicksJSON)
        XCTAssertEqual(referralClicks.clicks["click-1"]?.landingUrl, "https://voucha.ai/@alice")
        XCTAssertEqual(referralClicks.users["user-1"]?.username, "alice")

        let anonymousReferralClicksJSON = Data(
            #"{"results":[{"id":"click-2"}],"clicks":{"click-2":{"id":"click-2","landing_url":"https://voucha.ai/signup","signed_up_at":"2026-03-01T11:56:00Z","user_id":"user-2","created_at":"2026-03-01T11:55:00Z"}},"users":{"user-2":{"id":"user-2","roles":[],"profile_image_id":null,"markdown":null}},"page_info":{"has_next_page":false,"start_cursor":"click-2","end_cursor":null}}"#
                .utf8
        )
        let anonymousReferralClicks = try decoder.decode(
            ReferralClickLogResponse.self,
            from: anonymousReferralClicksJSON
        )
        XCTAssertNil(anonymousReferralClicks.users["user-2"]?.username)

        let plansJSON = ApiFixtureLoader.data("native.memberships.plans.default")
        let plans = try decoder.decode(MembershipPlansResponse.self, from: plansJSON)
        XCTAssertEqual(plans.plans["plus"]?.first?.stripePriceId, "price_native_plus_monthly")
        XCTAssertNil(plans.plans["pro"])
        XCTAssertEqual(plans.benefitCatalog?.version, 1)
        XCTAssertEqual(plans.benefitCatalog?.groups.first?.benefits.first?.id, "public_contribution_access")
        XCTAssertEqual(plans.benefitCatalog?.groups.flatMap(\.benefits).count, 11)

        let grant = try decoder.decode(
            MembershipGrantResponse.self,
            from: Data(#"{"grant":{"id":"grant-1"},"membership":{"id":"membership-1"},"queued":false}"#.utf8)
        )
        XCTAssertEqual(grant.grant.id, "grant-1")
        XCTAssertEqual(grant.membership.id, "membership-1")
        XCTAssertFalse(grant.queued)
    }

    func testDecodesSharedReferralLinkFixtures() throws {
        let decoder = makeVouchaDecoder()

        let feed = try decoder.decode(
            ReferralLinkFeedResponse.self,
            from: ApiFixtureLoader.data("web.referral-links.feed.default")
        )
        XCTAssertEqual(feed.results.first?.id, "referral-link-1")
        XCTAssertEqual(feed.results.first?.referralProgramId, "referral-program-1")
        XCTAssertEqual(feed.results.first?.referralProgramSlug, "test-card")

        let mine = try decoder.decode(
            Page<NativeReferralLink>.self,
            from: ApiFixtureLoader.data("native.referral-links.mine.default")
        )
        XCTAssertEqual(mine.results.first?.id, "referral-link-1")
        XCTAssertEqual(mine.results.first?.referralProgramName, "Test Card")
        XCTAssertNil(mine.results.first?.deactivatedAt)

        let trending = try decoder.decode(
            TrendingReferralProgramsResponse.self,
            from: ApiFixtureLoader.data("web.trending-referral-programs.default")
        )
        XCTAssertEqual(trending.referralPrograms.first?.id, "referral-program-1")
        XCTAssertEqual(trending.referralPrograms.first?.trendingScore, 9)
        XCTAssertEqual(trending.referralPrograms.first?.linkCount, 2)

        let prioritized = try decoder.decode(
            PrioritizedReferralLinksResponse.self,
            from: ApiFixtureLoader.data("web.referral-links.prioritized.default")
        )
        XCTAssertEqual(prioritized.links.first?.id, "referral-link-1")
        XCTAssertEqual(prioritized.links.first?.bestScore, 4.8)
        XCTAssertEqual(prioritized.users["user-1"]?.username, "testuser")

        let referralProgramSearch = try decoder.decode(
            TopicSearchResponse.self,
            from: ApiFixtureLoader.data("web.topics.search.referral-programs.default")
        )
        XCTAssertEqual(referralProgramSearch.results.first?.id, "referral-program-1")
        XCTAssertEqual(referralProgramSearch.topics["referral-program-1"]?.topicType, "referral_program")

        let clickLog = try decoder.decode(
            ReferralClickLogResponse.self,
            from: ApiFixtureLoader.data("native.referral-clicks.mine.default")
        )
        XCTAssertEqual(clickLog.results.first?.id, "referral-click-1")
        XCTAssertEqual(clickLog.clicks["referral-click-1"]?.landingUrl, "https://voucha.ai/@alice")
        XCTAssertEqual(clickLog.users["user-2"]?.username, "newmember")
    }

    func testDecodesAppealsMessagingBookmarksAndMemberships() throws {
        let decoder = makeVouchaDecoder()
        let appealObject = #""id":"appeal-1","case_id":"case-1","appellant_id":"user-1","user_warning_id":null,"user_suspension_id":null,"community_ban_id":null,"post_id":null,"community_id":null,"post_removal_kind":null,"appeal_reason":"Please review this again.","status":"pending","recommended_action":null,"ai_public_response":null,"ai_internal_response":null,"model":null,"ai_drafted_at":null,"public_response":null,"internal_notes":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null,"resolved_at":null,"resolved_by_id":null,"resolution_action":null,"latest_lifecycle_change_id":null,"created_at":"2026-03-01T11:55:00Z","updated_at":"2026-03-01T11:55:00Z","is_overdue":false"#

        let appeal = try decoder.decode(
            ModerationAppealEnvelope.self,
            from: Data("{\"appeal\":{\(appealObject)}}".utf8)
        )
        XCTAssertEqual(appeal.appeal.status, .pending)
        XCTAssertEqual(appeal.appeal.appealReason, "Please review this again.")

        let appealList = try decoder.decode(
            ModerationAppealListResponse.self,
            from: Data(
                "{\"appeals\":[{\(appealObject)}],\"page_info\":{\"has_next_page\":false,\"start_cursor\":null,\"end_cursor\":null}}"
                    .utf8
            )
        )
        XCTAssertEqual(appealList.appeals.first?.id, "appeal-1")
        XCTAssertEqual(appealList.pageInfo.hasNextPage, false)

        let conversationsJSON = Data(
            #"{"results":[{"id":"conversation-1","title":"Chat","created_at":"2026-03-01T11:55:00Z","created_by_id":"user-1","updated_at":"2026-03-01T12:00:00Z","participant_usernames":["alice","bob"]}],"page_info":{"has_next_page":false,"start_cursor":"conversation-1","end_cursor":null}}"#
                .utf8
        )
        let conversations = try decoder.decode(Page<DirectConversation>.self, from: conversationsJSON)
        XCTAssertNil(conversations.results.first?.channelType)
        XCTAssertEqual(conversations.results.first?.participantUsernames?.count, 2)

        let chatConversationsJSON = Data(
            #"{"results":[{"id":"conversation-1","title":"Chat","created_at":"2026-03-01T11:55:00Z","created_by_id":"user-1","updated_at":"2026-03-01T12:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"last_response_id":"message-2"}],"page_info":{"has_next_page":false,"start_cursor":"conversation-1","end_cursor":null}}"#
                .utf8
        )
        let chatConversations = try decoder.decode(ChatConversationListResponse.self, from: chatConversationsJSON)
        XCTAssertEqual(chatConversations.results.first?.lastResponseId, "message-2")

        let messagesJSON = Data(
            #"{"results":[{"id":"message-1","conversation_id":"conversation-1","body_text":"hello","created_by_id":"user-1","sender_username":"alice","created_at":"2026-03-01T11:55:00Z","updated_at":"2026-03-01T11:55:00Z","deleted_at":null}],"page_info":{"has_next_page":false,"start_cursor":"message-1","end_cursor":null}}"#
                .utf8
        )
        let messages = try decoder.decode(Page<DirectMessage>.self, from: messagesJSON)
        XCTAssertEqual(messages.results.first?.bodyText, "hello")

        let chatMessagesJSON = Data(
            #"{"results":[{"id":"message-1","conversation_id":"conversation-1","created_at":"2026-03-01T11:55:00Z","created_by_id":"user-1","updated_at":"2026-03-01T11:55:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":"hello","error":null}}],"page_info":{"has_next_page":false,"start_cursor":"message-1","end_cursor":null}}"#
                .utf8
        )
        let chatMessages = try decoder.decode(ChatMessagesResponse.self, from: chatMessagesJSON)
        XCTAssertEqual(chatMessages.results.first?.content.role, "assistant")

        let participantsJSON = Data(
            #"{"results":[{"id":"participant-1","conversation_id":"conversation-1","user_id":"user-1","role":"owner","created_at":"2026-03-01T11:55:00Z","removed_at":null}],"page_info":{"has_next_page":false,"start_cursor":"participant-1","end_cursor":null}}"#
                .utf8
        )
        let participants = try decoder.decode(Page<ConversationParticipant>.self, from: participantsJSON)
        XCTAssertEqual(participants.results.first?.role, "owner")

        let bookmarks = try decoder.decode(
            EntityBookmarksResponse.self,
            from: Data(#"{"bookmarks":{"follow":true,"mute":false}}"#.utf8)
        )
        XCTAssertEqual(bookmarks.bookmarks["follow"], true)
        XCTAssertEqual(bookmarks.bookmarks["mute"], false)

        let membershipsJSON = Data(
            #"{"results":[{"id":"membership-1","community_id":"community-1","user_id":"user-1","role":"member","approved_by_id":null,"created_at":"2026-03-01T11:55:00Z","updated_at":"2026-03-01T11:55:00Z","removed_at":null,"removed_by_id":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                .utf8
        )
        let memberships = try decoder.decode(Page<CommunityMembership>.self, from: membershipsJSON)
        XCTAssertEqual(memberships.results.first?.role, "member")
    }
}

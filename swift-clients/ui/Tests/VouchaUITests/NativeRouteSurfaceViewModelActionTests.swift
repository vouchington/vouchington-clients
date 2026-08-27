import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteSurfaceViewModelActionTests: NativeRouteSurfaceViewModelTestCase {
    func testNotificationsLoadNativeInbox() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            Data("""
            {
              "results": [{ "id": "notification-1", "read_at": null }],
              "notifications": {
                "notification-1": {
                  "id": "notification-1",
                  "user_id": "user-1",
                  "entity_type": "post",
                  "post_id": "post-1",
                  "rss_feed_item_id": null,
                  "actor_user_id": "user-2",
                  "community_id": null,
                  "conversation_id": null,
                  "moderation_report_id": null,
                  "review_dispute_id": null,
                  "user_warning_id": null,
                  "actor_label": null,
                  "event_key": null,
                  "title": "Native notification",
                  "body": "Someone replied.",
                  "target_path": "/discussion/post-1",
                  "target_entity": null,
                  "target_intent": null,
                  "read_at": null,
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-01T00:00:00Z",
                  "pushed_at": null
                }
              }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .notifications), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/notifications")
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "bell", title: "Native notification", detail: "Someone replied.")
        )
    }

    func testCompareDestinationExposesNativeActionsAndLoadsTopics() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (topicsBootstrapData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/compare"] = (topicsCompareData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .compare), client: makeClient())

        XCTAssertEqual(viewModel.actions.map { uiEnglish($0.title) }, ["Compare top topics", "Compare top domains"])

        await viewModel.perform(action: .compareTopics)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/topics", "/api/v1/topics/compare"])
        let compareQuery = CannedFeedURLProtocol.capturedURLs.last.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(compareQuery?.queryItems?.first(where: { $0.name == "slugs" })?.value, "swift,rust")
        XCTAssertEqual(viewModel.rows.map(\.title), ["Swift vs Rust", "Topic comparison"])
        XCTAssertEqual(viewModel.rows.first?.detail, "tag · tag")
    }

    func testCompareDestinationLoadsHostnames() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/top"] = (hostnamesBootstrapData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/hostnames/compare"] = (hostnamesCompareData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .compare), client: makeClient())

        await viewModel.perform(action: .compareHostnames)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/hostnames/top",
            "/api/v1/hostnames/compare"
        ])
        let compareQuery = CannedFeedURLProtocol.capturedURLs.last.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(compareQuery?.queryItems?.first(where: { $0.name == "ids" })?.value, "hostname-1,hostname-2")
        XCTAssertEqual(viewModel.rows.map(\.title), ["example.com vs example.org", "Hostname comparison"])
        XCTAssertEqual(viewModel.rows.first?.detail, "topic-1 · Unassigned")
    }

    func testCommunitiesLoadUsesPublicCommunitiesEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (communitiesData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .communitiesBrowse), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/communities")
        XCTAssertEqual(viewModel.rows.first?.title, "Native Community")
        XCTAssertEqual(viewModel.rows.first?.detail, "builders")
    }

    func testMessagesLoadUsesNativeDirectMessageEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages"] = (conversationData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .messages), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/messages")
        XCTAssertEqual(viewModel.rows.first?.title, "Support follow-up")
        XCTAssertEqual(viewModel.rows.first?.detail, "alice, bob")
    }

    func testMessagesModmailRouteLoadsCommunityModmailMessages() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities/example/modmail/thread-1/messages"] = (
            modmailMessagesData,
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/modmail/example/thread-1")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .messages),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.first?.path,
            "/api/v1/communities/example/modmail/thread-1/messages"
        )
        XCTAssertFalse(
            CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/my/messages" }
        )
        XCTAssertEqual(viewModel.rows.first?.title, "Modmail for example")
        XCTAssertEqual(
            viewModel.rows.last,
            verbatimRow(icon: "bubble.left", title: "moderator", detail: "Please review.")
        )
    }

    func testChatLoadsNativeConversationEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/conversations"] = (conversationData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .chat), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/conversations")
        XCTAssertEqual(viewModel.rows.first?.title, "Support follow-up")
        XCTAssertEqual(viewModel.rows.first?.icon, "bubble.left.and.bubble.right")
    }

    func testSupportLoadsNativeSupportThreadEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads"] = (supportThreadsData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .support), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/support-threads")
        XCTAssertEqual(viewModel.rows.first?.title, "Support request")
        let row = try XCTUnwrap(viewModel.rows.first)
        XCTAssertEqual(row.localizedDetail(locale: Locale(identifier: "en")), "Open")
        XCTAssertEqual(row.localizedDetail(locale: Locale(identifier: "fr")), "Ouvert")
    }

    func testMessageDetailLoadsMatchedConversation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/messages"] = (conversationMessagesData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/messages/conversation-1/participants"] = (conversationUsersData, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .messages),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(\.path)), Set([
            "/api/v1/my/messages/conversation-1/messages",
            "/api/v1/my/messages/conversation-1/participants"
        ]))
        XCTAssertEqual(viewModel.rows.first?.title, "Conversation conversation-1")
        XCTAssertEqual(viewModel.rows.first?.detail, "2 participants")
        XCTAssertEqual(viewModel.rows.last, verbatimRow(icon: "bubble.left", title: "alice", detail: "Hello native"))
    }

    func testAppealsLoadUsesMineAppealsEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (appealData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .moderationCases), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/appeals")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "mine" })?.value, "true")
        XCTAssertEqual(viewModel.rows.first?.title, "case-1")
        XCTAssertEqual(viewModel.rows.first?.detail, "Pending")
    }

    func testDisputesLoadUsesMineDisputesEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (disputesData, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/disputes")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationCases),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/disputes")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "mine" })?.value, "true")
        XCTAssertEqual(viewModel.rows.first?.title, "post-1")
        XCTAssertEqual(viewModel.rows.first?.detail, "Pending")
    }

    private var topicsBootstrapData: Data {
        Data(
            #"{"results":[{"id":"topic-1"},{"id":"topic-2"}],"topics":{"topic-1":{"id":"topic-1","name":"Swift","slug":"swift","topic_type":"tag"},"topic-2":{"id":"topic-2","name":"Rust","slug":"rust","topic_type":"tag"}}}"#
                .utf8
        )
    }

    private var conversationMessagesData: Data {
        Data("""
        {
          "results": [
            {
              "id": "message-1",
              "conversation_id": "conversation-1",
              "body_text": "Hello native",
              "created_by_id": "user-1",
              "sender_username": "alice",
              "created_at": "2026-01-01T00:00:00Z",
              "updated_at": "2026-01-01T00:00:00Z",
              "deleted_at": null
            }
          ],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
        }
        """.utf8)
    }

    private var conversationUsersData: Data {
        Data("""
        {
          "results": [
            {
              "id": "participant-1",
              "conversation_id": "conversation-1",
              "user_id": "user-1",
              "role": "member",
              "created_at": "2026-01-01T00:00:00Z",
              "removed_at": null
            },
            {
              "id": "participant-2",
              "conversation_id": "conversation-1",
              "user_id": "user-2",
              "role": "member",
              "created_at": "2026-01-01T00:00:00Z",
              "removed_at": null
            }
          ],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
        }
        """.utf8)
    }

    private var topicsCompareData: Data {
        Data(
            #"{"topics":{"topic-1":{"id":"topic-1","name":"Swift","slug":"swift","topic_type":"tag"},"topic-2":{"id":"topic-2","name":"Rust","slug":"rust","topic_type":"tag"}}}"#
                .utf8
        )
    }

    private var hostnamesBootstrapData: Data {
        Data(
            #"{"results":[{"id":"hostname-1"},{"id":"hostname-2"}],"hostnames":{"hostname-1":{"id":"hostname-1","hostname":"example.com","topic_id":"topic-1"},"hostname-2":{"id":"hostname-2","hostname":"example.org","topic_id":null}}}"#
                .utf8
        )
    }

    private var hostnamesCompareData: Data {
        Data(
            #"{"hostnames":{"hostname-1":{"id":"hostname-1","hostname":"example.com","topic_id":"topic-1"},"hostname-2":{"id":"hostname-2","hostname":"example.org","topic_id":null}}}"#
                .utf8
        )
    }

    private var communitiesData: Data {
        Data(
            #"{"results":[{"id":"community-1"}],"communities":{"community-1":{"id":"community-1","name":"Native Community","slug":"builders","description":null,"summary":null}}}"#
                .utf8
        )
    }

    private var conversationData: Data {
        Data(
            #"{"results":[{"id":"conversation-1","channel_type":"direct","title":"Support follow-up","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","participant_usernames":["alice","bob"]}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }

    private var modmailMessagesData: Data {
        Data(
            #"{"results":[{"id":"message-1","conversation_id":"thread-1","body_text":"Please review.","created_by_id":"mod-1","sender_username":"moderator","created_at":"2026-01-01T00:00:00Z","updated_at":null,"deleted_at":null}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }

    private var supportThreadsData: Data {
        Data(
            #"{"results":[{"id":"thread-1","support_contact_id":"contact-1","subject":"Support request","conversation_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }

    private var appealData: Data {
        Data(
            #"{"appeals":[{"id":"appeal-1","case_id":"case-1","appellant_id":"user-1","user_warning_id":null,"user_suspension_id":null,"community_ban_id":null,"post_id":"post-1","community_id":null,"post_removal_kind":"platform","appeal_reason":"Please review.","status":"pending","recommended_action":null,"ai_public_response":null,"ai_internal_response":null,"model":null,"ai_drafted_at":null,"public_response":null,"internal_notes":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null,"resolved_at":null,"resolved_by_id":null,"resolution_action":null,"latest_lifecycle_change_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","is_overdue":false}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }

    private var disputesData: Data {
        Data(
            #"{"disputes":[{"id":"dispute-1","post_id":"post-1","topic_id":"topic-1","reason":"incorrect","claim_text":"Native post","disputant_user_id":"user-1","recommended_action":null,"is_overdue":false,"status":"pending","target_label":"Native post","target_path":"/review/post-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","ai_drafted_at":null,"ai_internal_response":null,"ai_public_response":null,"approved_at":null,"approved_by_id":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"internal_notes":null,"latest_lifecycle_change_id":null,"model":null,"public_response":null,"resolution_action":null,"resolved_at":null,"resolved_by_id":null,"sent_at":null}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }
}

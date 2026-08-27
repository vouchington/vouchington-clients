import Foundation
import ViewInspector
@testable import VouchaFeatures
@testable import VouchaLocalization
@testable import VouchaModels
import XCTest

@MainActor
final class NativeRouteSurfaceViewModelAgentSupportTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers["/api/v1/agents/helper"] = (
            ApiFixtureLoader.data("native.agents.detail.default"),
            200
        )
    }

    func testAgentsLoadNativeAgentDirectoryEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/agents"] = (ApiFixtureLoader.data("native.agents.default"), 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agents")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents),
            client: makeClient(),
            routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/agents")
        XCTAssertEqual(viewModel.rows.first?.title, "Fixture Agent User 001")
        let detail = try XCTUnwrap(viewModel.rows.first?.localizedDetail(
            locale: Locale(identifier: "en_US"),
            timeZone: .gmt
        ))
        XCTAssertTrue(detail.contains("moderator"))
        XCTAssertTrue(detail.contains("Active"))
        XCTAssertTrue(try detail.contains(UiMessages.date(
            XCTUnwrap(viewModel.agentDirectoryResults.first?.createdAt),
            date: .abbreviated,
            time: .omitted,
            locale: Locale(identifier: "en_US"),
            timeZone: .gmt
        )))
    }

    func testAgentConversationDeepLinkInitializesSupportedFilterAndControlState() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/agent/helper?username=ignored&user_id=fixture-user"
        ))
        CannedFeedURLProtocol.handlers["/api/v1/agents/helper"] = (
            ApiFixtureLoader.data("native.agents.detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/agents/helper/conversations"] = (
            ApiFixtureLoader.data("native.agents.conversations.default"), 200
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.agentConversationFilter, .userId("fixture-user"))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.first { $0.path == "/api/v1/agents/helper/conversations" }?.query,
            "limit=25&user_id=fixture-user"
        )
        let surface = NativeEngineeringAgentDetailSurface(viewModel: viewModel, onNavigate: { _ in })
        XCTAssertEqual(
            try surface.inspect().find(ViewType.TextField.self).accessibilityLabel().string(),
            "User ID value"
        )
        XCTAssertEqual(try surface.inspect().find(ViewType.TextField.self).input(), "fixture-user")
    }

    func testAgentConversationRouteQueryReplacesStaleFilterOnlyWhenRouteChanges() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agent/helper?username=deep-link"))
        let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
        viewModel.agentConversationFilter = .postSlug("stale")
        viewModel.agentConversationFilterRouteIdentity = "/agent/other|username=other"

        viewModel.hydrateAgentConversationFilterFromRoute()
        XCTAssertEqual(viewModel.agentConversationFilter, .username("deep-link"))

        viewModel.agentConversationFilter = .postSlug("user-selected")
        viewModel.hydrateAgentConversationFilterFromRoute()
        XCTAssertEqual(viewModel.agentConversationFilter, .postSlug("user-selected"))
    }

    func testAgentDetailLoadsAgentAndConversationsEndpoints() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/agents/helper"] = (
            ApiFixtureLoader.data("native.agents.detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/agents/helper/conversations"] = (
            ApiFixtureLoader.data("native.agents.conversations.default"), 200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agent/helper")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents),
            client: makeClient(),
            routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2

        await viewModel.load()

        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(\.path)), Set([
            "/api/v1/agents/helper",
            "/api/v1/agents/helper/conversations"
        ]))
        XCTAssertEqual(viewModel.agentDetail?.agent.slug, "helper")
        XCTAssertEqual(viewModel.rows.count, 2)
        XCTAssertEqual(viewModel.rows.first?.title, "Fixture conversation 012")
        XCTAssertTrue(viewModel.canLoadMoreAgentConversations)
    }

    func testAgentConversationDetailDecodesConversationAndMessagesEnvelope() async throws {
        let conversationId = "00000000-0000-7000-8000-000000000101"
        let path = "/api/v1/agents/helper/conversations/\(conversationId)"
        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("native.agents.conversation.default"),
            200
        )
        let match = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/agent/helper/conversation/\(conversationId)")?.match
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents),
            client: makeClient(),
            routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2

        await viewModel.load()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.map(\.path).contains(path))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.map(\.path).contains("/api/v1/agents/helper"))
        XCTAssertEqual(viewModel.agentConversationAgentSystemUserId, "00000000-0000-7000-8000-000000000001")
        XCTAssertEqual(
            viewModel.rows.map(\.title),
            [
                "Fixture agent conversation",
                "Deleted",
                "Agent"
            ]
        )
        XCTAssertTrue(viewModel.canLoadOlderAgentConversationMessages)

        CannedFeedURLProtocol.queuedHandlers[path] = [(
            ApiFixtureLoader.data("native.agents.conversation.page-2"),
            200,
            0.1
        )]
        let first = Task { await viewModel.loadOlderAgentConversationMessages() }
        let second = Task { await viewModel.loadOlderAgentConversationMessages() }
        await first.value
        await second.value

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.last?.query,
            "limit=2&after=eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoie1wiYWdlbnRJZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMzAyXCIsXCJjb252ZXJzYXRpb25JZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMTAxXCIsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9"
        )
        XCTAssertEqual(
            viewModel.rows.map(\.title),
            [
                "Fixture agent conversation",
                "Agent",
                "Deleted",
                "Agent"
            ]
        )
        XCTAssertEqual(viewModel.agentConversationMessages.count, 3)
        XCTAssertEqual(
            viewModel.agentConversationMessages.first(where: {
                $0.id == "00000000-0000-7000-8000-000000000201"
            })?.content?.content,
            "Hello"
        )
        XCTAssertFalse(viewModel.canLoadOlderAgentConversationMessages)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 3)
    }

    func testAgentConversationPaginationPreservesRowsAndRetriesAfterFailure() async throws {
        let path = "/api/v1/agents/helper/conversations/conversation-1"
        CannedFeedURLProtocol.handlers[path] = (agentConversationDetailData, 200)
        let match = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/agent/helper/conversation/conversation-1")?.match
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents),
            client: makeClient(),
            routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2
        await viewModel.load()
        let loadedTitles = viewModel.rows.map(\.title)

        CannedFeedURLProtocol.handlers[path] = (Data(#"{"message":"failed"}"#.utf8), 500)
        await viewModel.loadOlderAgentConversationMessages()

        XCTAssertEqual(viewModel.rows.map(\.title), loadedTitles)
        XCTAssertNotNil(viewModel.agentConversationPaginationErrorMessage)
        XCTAssertTrue(viewModel.canLoadOlderAgentConversationMessages)

        CannedFeedURLProtocol.handlers[path] = (agentConversationOlderPageData, 200)
        await viewModel.loadOlderAgentConversationMessages()
        XCTAssertNil(viewModel.agentConversationPaginationErrorMessage)
        XCTAssertEqual(viewModel.rows.map(\.title).first, "Conversation with helper")
    }

    func testAgentConversationStalePageCannotClearNewPageLoadingState() async throws {
        let path = "/api/v1/agents/helper/conversations/conversation-1"
        CannedFeedURLProtocol.handlers[path] = (agentConversationDetailData, 200)
        let match = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/agent/helper/conversation/conversation-1")?.match
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents),
            client: makeClient(),
            routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2
        await viewModel.load()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (agentConversationStalePageData, 200, 0.1),
            (agentConversationDetailData, 200, 0),
            (agentConversationOlderPageData, 200, 0.2)
        ]

        let stale = Task { await viewModel.loadOlderAgentConversationMessages() }
        try await waitForRequest(path, count: 2)
        viewModel.state = .idle
        await viewModel.load()
        CannedFeedURLProtocol.suspendResponse(path: path)
        let currentResponse = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let current = Task { await viewModel.loadOlderAgentConversationMessages() }
        _ = try await currentResponse.wait()

        XCTAssertTrue(viewModel.isLoadingOlderAgentConversationMessages)

        await stale.value

        XCTAssertTrue(viewModel.isLoadingOlderAgentConversationMessages)

        CannedFeedURLProtocol.releaseResponse(path: path)
        await current.value

        XCTAssertFalse(viewModel.rows.map(\.detail).contains("message-stale"))
        XCTAssertTrue(viewModel.agentConversationMessages.map(\.id).contains("message-0"))
        XCTAssertFalse(viewModel.isLoadingOlderAgentConversationMessages)
    }

    func testAgentConversationReplacementInitialLoadInvalidatesOlderContinuationBeforeAwait() async throws {
        let path = "/api/v1/agents/helper/conversations/conversation-1"
        CannedFeedURLProtocol.handlers[path] = (agentConversationDetailData, 200)
        let match = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/agent/helper/conversation/conversation-1")?.match
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents),
            client: makeClient(),
            routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2
        await viewModel.load()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (agentConversationStalePageData, 200, 0.1),
            (agentConversationReplacementData, 200, 0.2)
        ]

        let staleContinuation = Task { await viewModel.loadOlderAgentConversationMessages() }
        try await waitForRequest(path, count: 2)
        viewModel.state = .idle
        let replacementInitialLoad = Task { await viewModel.load() }
        await staleContinuation.value
        await replacementInitialLoad.value

        XCTAssertEqual(viewModel.agentConversationMessages.map(\.id), ["message-replacement"])
        XCTAssertFalse(viewModel.rows.map(\.title).contains("message-stale"))
        XCTAssertFalse(viewModel.isLoadingOlderAgentConversationMessages)
    }

    func testEmptyAgentConversationFallbacksLocalizeWithoutTranslatingServerTitles() throws {
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents),
            client: nil
        )
        viewModel.agentConversationListResults = try [
            decode(
                AgentConversationSummary.self,
                """
                {
                  "id": "conversation-empty", "title": "",
                  "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
                  "updated_at": "2026-07-01T10:00:00Z", "updated_by_id": null,
                  "deleted_at": null, "deleted_by_id": null
                }
                """
            ),
            decode(
                AgentConversationSummary.self,
                """
                {
                  "id": "conversation-named", "title": "Server title",
                  "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
                  "updated_at": "2026-07-01T10:00:00Z", "updated_by_id": null,
                  "deleted_at": null, "deleted_by_id": null
                }
                """
            )
        ]

        let listRows = viewModel.agentConversationListRows()

        XCTAssertEqual(listRows[0].localizedTitle(locale: Locale(identifier: "es")), "Conversación del agente")
        XCTAssertEqual(listRows[1].localizedTitle(locale: Locale(identifier: "es")), "Server title")

        viewModel.agentConversation = try decode(
            AgentConversation.self,
            """
            {
              "id": "conversation-empty", "channel_type": "agent", "title": "",
              "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:00:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null, "last_response_id": null
            }
            """
        )

        XCTAssertEqual(
            viewModel.agentConversationRows(fallbackId: "conversation-empty", agentSystemUserId: nil)[0]
                .localizedTitle(locale: Locale(identifier: "es")),
            "Conversación del agente conversation-empty"
        )
    }

    private var agentData: Data {
        Data(#"{"agent":{"id":"agent-1","name":"Native Helper","slug":"helper","summary":"Answers questions"}}"#.utf8)
    }

    private func decode<T: Decodable>(_ type: T.Type, _ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(type, from: Data(json.utf8))
    }

    private func waitForRequest(_ path: String, count: Int) async throws {
        for _ in 0 ..< 250 {
            if CannedFeedURLProtocol.capturedPathCount(path) >= count {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for captured path \(path)")
    }

    private var agentConversationDetailData: Data {
        Data("""
        {
          "conversation": {
            "id": "conversation-1", "channel_type": "agent", "title": "Conversation with helper",
            "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
            "updated_at": "2026-07-01T10:02:00Z", "updated_by_id": null,
            "deleted_at": null, "deleted_by_id": null, "last_response_id": "message-2"
          },
          "results": [
            {
              "id": "message-1", "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:01:00Z", "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:01:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null,
              "content": { "role": "user", "content": "Hello" }
            },
            {
              "id": "message-2", "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:02:00Z", "created_by_id": "agent-1",
              "updated_at": "2026-07-01T10:02:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null,
              "content": { "role": "assistant", "content": "Hi" }
            }
          ],
          "page_info": {
            "has_next_page": true,
            "end_cursor": "older-cursor",
            "start_cursor": "newer-cursor"
          }
        }
        """.utf8)
    }

    private var agentConversationOlderPageData: Data {
        Data("""
        {
          "conversation": {
            "id": "conversation-1", "channel_type": "agent", "title": "Conversation with helper",
            "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
            "updated_at": "2026-07-01T10:02:00Z", "updated_by_id": null,
            "deleted_at": null, "deleted_by_id": null, "last_response_id": "message-2"
          },
          "results": [
            {
              "id": "message-0", "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:00:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null,
              "content": { "role": "user", "content": "Earlier" }
            },
            {
              "id": "message-0", "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:00:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null,
              "content": { "role": "user", "content": "Earlier duplicate" }
            },
            {
              "id": "message-1", "conversation_id": "conversation-1",
              "created_at": "2026-07-01T10:01:00Z", "created_by_id": "user-1",
              "updated_at": "2026-07-01T10:01:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null,
              "content": { "role": "user", "content": "Hello duplicate" }
            }
          ],
          "page_info": {
            "has_next_page": false,
            "end_cursor": null,
            "start_cursor": "older-cursor"
          }
        }
        """.utf8)
    }

    private var agentConversationStalePageData: Data {
        Data("""
        {
          "conversation": {
            "id": "conversation-1", "channel_type": "agent", "title": "Conversation with helper",
            "created_at": "2026-07-01T10:00:00Z", "created_by_id": "user-1",
            "updated_at": "2026-07-01T10:02:00Z", "updated_by_id": null,
            "deleted_at": null, "deleted_by_id": null, "last_response_id": "message-2"
          },
          "results": [
            {
              "id": "message-stale", "conversation_id": "conversation-1",
              "created_at": "2026-07-01T09:59:00Z", "created_by_id": "user-1",
              "updated_at": "2026-07-01T09:59:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null,
              "content": { "role": "user", "content": "Stale" }
            }
          ],
          "page_info": {
            "has_next_page": false,
            "end_cursor": null,
            "start_cursor": "older-cursor"
          }
        }
        """.utf8)
    }

    private var agentConversationReplacementData: Data {
        Data("""
        {
          "conversation": {
            "id": "conversation-1", "channel_type": "agent", "title": "Replacement conversation",
            "created_at": "2026-07-01T11:00:00Z", "created_by_id": "user-1",
            "updated_at": "2026-07-01T11:01:00Z", "updated_by_id": null,
            "deleted_at": null, "deleted_by_id": null, "last_response_id": "message-replacement"
          },
          "results": [
            {
              "id": "message-replacement", "conversation_id": "conversation-1",
              "created_at": "2026-07-01T11:01:00Z", "created_by_id": "agent-1",
              "updated_at": "2026-07-01T11:01:00Z", "updated_by_id": null,
              "deleted_at": null, "deleted_by_id": null,
              "content": { "role": "assistant", "content": "Replacement" }
            }
          ],
          "page_info": {
            "has_next_page": false,
            "end_cursor": null,
            "start_cursor": "replacement-cursor"
          }
        }
        """.utf8)
    }
}

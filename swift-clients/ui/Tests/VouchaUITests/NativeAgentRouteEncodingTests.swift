import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeAgentRouteEncodingTests: NativeRouteSurfaceViewModelTestCase {
    func testEncodedAgentDetailRouteRequestsEachEndpointOnce() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agent/foo%20bar"))
        let client = try makeClient()
        CannedFeedURLProtocol.handlers = [
            "/api/v1/agents/foo bar": (agentData, 200),
            "/api/v1/agents/foo bar/conversations": (conversationListData, 200)
        ]
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: client,
            routeMatch: route.match
        )

        _ = try await viewModel.loadAgentRows(client: client)

        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(encodedPath)), [
            "/api/v1/agents/foo%20bar",
            "/api/v1/agents/foo%20bar/conversations"
        ])
    }

    func testEncodedAgentConversationRouteRequestsEachEndpointOnce() async throws {
        let route = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/agent/foo%20bar/conversation/conversation%2Fone")
        )
        let client = try makeClient()
        CannedFeedURLProtocol.handlers = [
            "/api/v1/agents/foo bar": (agentData, 200),
            "/api/v1/agents/foo bar/conversations/conversation/one": (conversationData, 200)
        ]
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: client,
            routeMatch: route.match
        )

        _ = try await viewModel.loadAgentRows(client: client)

        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(encodedPath)), [
            "/api/v1/agents/foo%20bar",
            "/api/v1/agents/foo%20bar/conversations/conversation%2Fone"
        ])
    }

    func testEncodedAgentSlashAndPercentReachEachEndpointOnce() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agent/foo%2Fbar%25baz"))
        let client = try makeClient()
        CannedFeedURLProtocol.handlers = [
            "/api/v1/agents/foo/bar%baz": (agentData, 200),
            "/api/v1/agents/foo/bar%baz/conversations": (conversationListData, 200)
        ]
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: client,
            routeMatch: route.match
        )

        _ = try await viewModel.loadAgentRows(client: client)

        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(encodedPath)), [
            "/api/v1/agents/foo%2Fbar%25baz",
            "/api/v1/agents/foo%2Fbar%25baz/conversations"
        ])
    }

    private var agentData: Data {
        Data(
            #"{"agent":{"id":"agent","system_user_id":"system","agent_type":"helper","activated_at":null,"deactivated_at":null,"created_at":"2026-07-01T10:00:00Z","updated_at":"2026-07-01T10:00:00Z","deleted_at":null,"slug":null,"moderator":null},"user":null}"#
                .utf8
        )
    }

    private var conversationListData: Data {
        Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null},"users":{}}"#
            .utf8)
    }

    private var conversationData: Data {
        Data(
            #"{"conversation":{"id":"conversation/one","channel_type":"agent","title":"Conversation","created_at":"2026-07-01T10:00:00Z","created_by_id":"user","updated_at":"2026-07-01T10:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"last_response_id":null},"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8
        )
    }

    private func encodedPath(_ url: URL) -> String {
        URLComponents(url: url, resolvingAgainstBaseURL: false)?.percentEncodedPath ?? ""
    }
}

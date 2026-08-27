import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeAgentListPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testAgentDirectoryLoadsFixtureContinuationOnce() async throws {
        let path = "/api/v1/agents"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("native.agents.default"), 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agents")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents), client: makeClient(), routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2
        await viewModel.load()
        CannedFeedURLProtocol.queuedHandlers[path] = [(
            ApiFixtureLoader.data("native.agents.page-2"), 200, 0.1
        )]

        async let first: Void = viewModel.loadMoreAgents()
        async let duplicate: Void = viewModel.loadMoreAgents()
        _ = await (first, duplicate)

        XCTAssertEqual(viewModel.rows.count, 3)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.last?.query,
            "limit=2&after=eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDMwMSIsInNjb3BlIjoiYWdlbnQtZGlyZWN0b3J5OmlkLWRlc2MifQ"
        )
        XCTAssertFalse(viewModel.canLoadMoreAgents)
    }

    func testAgentDirectoryMergesInitialAndContinuationUsersForRowTitles() async throws {
        let path = "/api/v1/agents"
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("native.agents.default"), 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agents")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents), client: makeClient(), routeMatch: match
        )
        viewModel.agentListPageSize = 2

        await viewModel.load()
        CannedFeedURLProtocol.queuedHandlers[path] = [(
            ApiFixtureLoader.data("native.agents.page-2"), 200, 0
        )]
        await viewModel.loadMoreAgents()

        XCTAssertEqual(
            viewModel.rows.map(\.title),
            ["Fixture Agent User 001", "Fixture Agent User 002", "Fixture Agent User 003"]
        )
        XCTAssertEqual(viewModel.agentDirectoryUsers.count, 3)
    }

    func testAgentConversationListPreservesRowsAcrossFailureAndFixtureRetry() async throws {
        let path = "/api/v1/agents/helper/conversations"
        CannedFeedURLProtocol.handlers["/api/v1/agents/helper"] = (
            ApiFixtureLoader.data("native.agents.detail.default"), 200
        )
        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("native.agents.conversations.default"), 200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/agent/helper")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .engineeringAgents), client: makeClient(), routeMatch: match
        )
        viewModel.agentListPageSize = 2
        viewModel.agentMessagePageSize = 2
        await viewModel.load()
        let currentRows = viewModel.rows

        CannedFeedURLProtocol.handlers[path] = (Data(#"{"message":"offline"}"#.utf8), 500)
        await viewModel.loadMoreAgentConversations()
        XCTAssertEqual(viewModel.rows, currentRows)
        XCTAssertNotNil(viewModel.agentConversationsPageErrorMessage)

        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("native.agents.conversations.page-2"), 200
        )
        await viewModel.loadMoreAgentConversations()

        XCTAssertEqual(viewModel.rows.count, 3)
        XCTAssertNil(viewModel.agentConversationsPageErrorMessage)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.last?.query,
            "limit=2&after=eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMTAxMSIsInNjb3BlIjoie1wiYWdlbnRTeXN0ZW1Vc2VySWRcIjpcIjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMVwiLFwidXNlcklkXCI6bnVsbCxcInBvc3RJZFwiOm51bGwsXCJyc3NGZWVkSXRlbUlkXCI6bnVsbCxcIm9ubHlMaW5rZWRcIjp0cnVlLFwib3JkZXJcIjpcImlkLWRlc2NcIn0ifQ"
        )
    }
}

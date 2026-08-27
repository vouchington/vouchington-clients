import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteForwardPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testNotificationsAppendByStableIdAndForwardExactCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/notifications"] = [
            (notificationPage(ids: ["one"], cursor: "next", hasMore: true), 200, 0),
            (notificationPage(ids: ["one", "two"], cursor: nil, hasMore: false), 200, 0)
        ]
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .notifications), client: makeClient())

        await viewModel.load()
        await viewModel.loadMoreForwardRows()

        XCTAssertEqual(viewModel.rows.map(\.title), ["Notification one", "Notification two"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&after=next")
    }

    func testContinuationFailurePreservesRowsAndRetriesSameCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/notifications"] = [
            (notificationPage(ids: ["one"], cursor: "retry-me", hasMore: true), 200, 0),
            (Data("{}".utf8), 503, 0),
            (notificationPage(ids: ["two"], cursor: nil, hasMore: false), 200, 0)
        ]
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .notifications), client: makeClient())

        await viewModel.load()
        await viewModel.loadMoreForwardRows()
        XCTAssertEqual(viewModel.rows.map(\.title), ["Notification one"])
        XCTAssertNotNil(viewModel.forwardPagination.lastError)

        await viewModel.loadMoreForwardRows()
        XCTAssertEqual(viewModel.rows.map(\.title), ["Notification one", "Notification two"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.suffix(2).map(\.query), [
            "limit=25&after=retry-me",
            "limit=25&after=retry-me"
        ])
    }

    private func notificationPage(ids: [String], cursor: String?, hasMore: Bool) -> Data {
        let results = ids.map { #"{"id":"\#($0)"}"# }.joined(separator: ",")
        let notifications = ids.map { id in
            #""\#(id)":{"id":"\#(id)","user_id":"user","entity_type":"follow","post_id":null,"rss_feed_item_id":null,"actor_user_id":null,"community_id":null,"conversation_id":null,"moderation_report_id":null,"review_dispute_id":null,"user_warning_id":null,"actor_label":null,"event_key":null,"title":"Notification \#(id)","body":"Body","target_path":null,"target_entity":null,"target_intent":null,"read_at":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","pushed_at":null}"#
        }.joined(separator: ",")
        let endCursor = cursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"results":[\#(results)],"notifications":{\#(notifications)},"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(endCursor)}}"#
                .utf8
        )
    }

    func testUsersBrowseAppendsByStableIdAndForwardsExactCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (usersPage(ids: ["user-1"], cursor: "next", hasMore: true), 200, 0),
            (usersPage(ids: ["user-1", "user-2"], cursor: nil, hasMore: false), 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/users?q=alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()
        await viewModel.loadMoreForwardRows()

        XCTAssertEqual(viewModel.rows.map(\.title), ["user-1", "user-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "q=alice&limit=25&after=next")
    }

    func testUsersBrowseContinuationFailurePreservesRowsAndRetriesSameCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users"] = [
            (usersPage(ids: ["user-1"], cursor: "retry-me", hasMore: true), 200, 0),
            (Data("{}".utf8), 503, 0),
            (usersPage(ids: ["user-1", "user-2"], cursor: nil, hasMore: false), 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/users?q=alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()
        await viewModel.loadMoreForwardRows()
        XCTAssertEqual(viewModel.rows.map(\.title), ["user-1"])
        XCTAssertNotNil(viewModel.forwardPagination.lastError)

        await viewModel.loadMoreForwardRows()
        XCTAssertEqual(viewModel.rows.map(\.title), ["user-1", "user-2"])
    }

    private func usersPage(ids: [String], cursor: String?, hasMore: Bool) -> Data {
        let results = ids.map { #"{"id":"\#($0)","username":"\#($0)"}"# }.joined(separator: ",")
        let endCursor = cursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"results":[\#(results)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(endCursor)}}"#
                .utf8
        )
    }
}

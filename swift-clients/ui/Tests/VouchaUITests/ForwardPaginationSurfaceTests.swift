import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class ForwardPaginationSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityBrowseAppendsSecondPageAndDeduplicatesRows() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/communities"] = [
            (communityPage(ids: ["one"], hasMore: true, cursor: "next"), 200, 0),
            (communityPage(ids: ["one", "two"], hasMore: false, cursor: nil), 200, 0)
        ]
        let viewModel = try CommunityBrowseViewModel(client: makeClient())

        await viewModel.search(query: "swift")
        await viewModel.loadNextPage()

        XCTAssertEqual(viewModel.rows.map(\.id), ["one", "two"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&q=swift&after=next")
    }

    func testCommunityBrowseSearchResetRejectsStalePage() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/communities"] = [
            (communityPage(ids: ["stale"], hasMore: false, cursor: nil), 200, 0.15),
            (communityPage(ids: ["fresh"], hasMore: false, cursor: nil), 200, 0)
        ]
        let viewModel = try CommunityBrowseViewModel(client: makeClient())

        let stale = Task { await viewModel.search(query: "old") }
        try await Task.sleep(nanoseconds: 25_000_000)
        await viewModel.search(query: "new")
        await stale.value

        XCTAssertEqual(viewModel.rows.map(\.id), ["fresh"])
    }

    func testListsAppendSecondListPageAndSelectedListItems() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/lists"] = [
            (listPage(id: "list-one", hasMore: true, cursor: "lists-next"), 200, 0),
            (listPage(id: "list-two", hasMore: false, cursor: nil), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/lists/list-one/items"] = [
            (itemPage(id: "item-one", hasMore: true, cursor: "items-next"), 200, 0),
            (itemPage(id: "item-two", hasMore: false, cursor: nil), 200, 0)
        ]
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.loadMoreLists()
        await viewModel.loadMoreItems()

        XCTAssertEqual(viewModel.lists.map(\.id), ["list-one", "list-two"])
        XCTAssertEqual(viewModel.items.map(\.id), ["item-one", "item-two"])
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.query?.contains("after=lists-next") == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.query?.contains("after=items-next") == true })
    }

    func testListItemFilterResetRejectsStalePage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (listPage(
            id: "list-one",
            hasMore: false,
            cursor: nil
        ), 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-one/items"] = (
            itemPage(id: "initial", hasMore: false, cursor: nil),
            200
        )
        let viewModel = try ListsViewModel(client: makeClient())
        await viewModel.load()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/lists/list-one/items"] = [
            (itemPage(id: "stale", hasMore: false, cursor: nil), 200, 0.15),
            (itemPage(id: "fresh", hasMore: false, cursor: nil), 200, 0)
        ]

        let stale = Task { await viewModel.selectFilter(.reading) }
        try await Task.sleep(nanoseconds: 25_000_000)
        await viewModel.selectFilter(.watch)
        await stale.value

        XCTAssertEqual(viewModel.items.map(\.id), ["fresh"])
    }

    private func communityPage(ids: [String], hasMore: Bool, cursor: String?) -> Data {
        let results = ids.map { #"{"id":"\#($0)"}"# }.joined(separator: ",")
        let communities = ids.map { id in
            #""\#(id)":{"id":"\#(id)","name":"\#(id)","slug":"\#(id)","markdown":null,"visibility":"public","member_roster_visibility":"public","list_type":null,"member_invites_allowed_at":null,"post_approval_required_at":null,"automod_action":"record_only","should_allow_review_posts":true,"should_allow_data_point_posts":true,"trusted_at":null,"profile_image_id":null,"banner_image_id":null,"created_by_id":"user","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","deleted_at":null,"deleted_by_id":null,"archived_at":null,"archived_by_id":null,"default_language":null,"lingua_rs_detected_language":null,"rules_markdown":null}"#
        }.joined(separator: ",")
        return Data(
            #"{"results":[\#(results)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(cursor.map { "\"\($0)\"" } ?? "null")},"communities":{\#(communities)}}"#
                .utf8
        )
    }

    private func listPage(id: String, hasMore: Bool, cursor: String?) -> Data {
        Data(
            #"{"results":[{"id":"\#(id)"}],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(cursor.map { "\"\($0)\"" } ?? "null")},"lists":{"\#(id)":{"id":"\#(id)","owner_user_id":"user","name":"\#(id)","description":null,"visibility":"private","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","removed_at":null}}}"#
                .utf8
        )
    }

    private func itemPage(id: String, hasMore: Bool, cursor: String?) -> Data {
        Data(
            #"{"results":[{"id":"\#(id)"}],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(cursor.map { "\"\($0)\"" } ?? "null")},"list_items":{"\#(id)":{"id":"\#(id)","list_id":"list-one","item_type":"post","entity_id":"\#(id)","order_index":0,"created_at":"2026-01-01T00:00:00Z","media_type":"video"}}}"#
                .utf8
        )
    }
}

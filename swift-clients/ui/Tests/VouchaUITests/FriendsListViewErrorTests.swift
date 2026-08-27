import Foundation
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class FriendsListViewErrorTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    private func makeViewModel() -> FriendsListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return FriendsListViewModel(client: apiClient, userId: "user-abc", config: config)
    }

    private func makeUsersPage(ids: [String], hasMore: Bool = false, endCursor: String? = nil) -> Data {
        let users = ids.map { id in
            "{\"id\":\"\(id)\",\"username\":\"user_\(id)\",\"roles\":[]}"
        }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {"results":[\(users)],"page_info":{"has_next_page":\(hasMore),"end_cursor":\(cursor),"start_cursor":null}}
        """.utf8)
    }

    func testLoadedListViewShowsLoadMoreErrorWhenAppendFails() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        let vm = makeViewModel()
        await vm.load()
        await vm.loadMore()
        let sut = FriendsListView(viewModel: vm)

        XCTAssertEqual(try sut.inspect().find(text: "Try Again").string(), "Try Again")
    }

    func testLoadMoreErrorIsHiddenWhileRetryIsLoading() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (Data("{}".utf8), 500, 0),
            (makeUsersPage(ids: ["f2"], hasMore: false), 200, 0.2)
        ]
        let vm = makeViewModel()
        await vm.load()
        await vm.loadMore()

        XCTAssertNotNil(vm.loadMoreError)

        let retry = Task { await vm.loadMore() }
        try await Task.sleep(nanoseconds: 50_000_000)

        XCTAssertTrue(vm.isLoadingMore)
        XCTAssertNil(vm.loadMoreError)

        await retry.value
        XCTAssertEqual(vm.items.map(\.id), ["f1", "f2"])
    }

    func testLoadMoreErrorShowsAfterVisibleRowsAreRemoved() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/f1/follow"] = (Data("{}".utf8), 200)
        let vm = makeViewModel()
        await vm.load()
        await vm.toggleFollow(userId: "f1")
        await vm.loadMore()
        let sut = FriendsListView(viewModel: vm)

        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertTrue(vm.hasMore)
        XCTAssertNotNil(vm.loadMoreError)
        XCTAssertFalse(vm.listErrorRetryLoadsInitialPage)
        XCTAssertEqual(try sut.inspect().find(text: "Try Again").string(), "Try Again")
    }
}

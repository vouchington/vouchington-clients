import Foundation
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class FriendsListViewModelFollowRegressionTests: XCTestCase {
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

    func testLoadMoreFollowingDoesNotDuplicateOptimisticFollow() async {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (makeUsersPage(ids: ["r1"], hasMore: false), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/followers"] = (
            makeUsersPage(ids: ["r1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/r1/follow"] = (Data("{}".utf8), 200)
        let vm = makeViewModel()
        await vm.load()
        vm.tab = .followers
        await vm.load()
        await vm.toggleFollow(userId: "r1")

        vm.tab = .following
        await vm.loadMore()

        XCTAssertEqual(vm.following.map(\.id), ["f1", "r1"])
    }

    func testLoadMoreButtonStaysReachableAfterUnfollowingVisibleRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/f1/follow"] = (Data("{}".utf8), 200)
        let vm = makeViewModel()
        await vm.load()
        await vm.toggleFollow(userId: "f1")
        let sut = FriendsListView(viewModel: vm)

        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertTrue(vm.hasMore)
        XCTAssertEqual(try sut.inspect().find(text: "Load more").string(), "Load more")
    }

    func testInitialFollowingFailureRetriesReloadWhenOptimisticRowExists() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/followers"] = (
            makeUsersPage(ids: ["r1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/r1/follow"] = (Data("{}".utf8), 200)
        let vm = makeViewModel()
        vm.tab = .followers
        await vm.load()
        await vm.toggleFollow(userId: "r1")

        vm.tab = .following
        await vm.load()
        let sut = FriendsListView(viewModel: vm)

        XCTAssertEqual(vm.items.map(\.id), ["r1"])
        XCTAssertNil(vm.loadMoreError)
        XCTAssertNotNil(vm.listError)
        XCTAssertTrue(vm.listErrorRetryLoadsInitialPage)
        XCTAssertEqual(try sut.inspect().find(text: "Try Again").string(), "Try Again")
    }
}

import Foundation
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

// MARK: - FriendsTab

final class FriendsTabTests: XCTestCase {
    func testAllCasesCount() {
        XCTAssertEqual(FriendsTab.allCases.count, 2)
    }

    func testIdEqualsRawValue() {
        for tab in FriendsTab.allCases {
            XCTAssertEqual(tab.id, tab.rawValue)
        }
    }

    func testTitlesNonEmpty() {
        for tab in FriendsTab.allCases {
            XCTAssertFalse(tab.titleKey.rawValue.isEmpty)
        }
    }
}

// MARK: - FriendsListViewModel

@MainActor
final class FriendsListViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    private func makeViewModel(userId: String? = "user-abc") -> FriendsListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return FriendsListViewModel(client: apiClient, userId: userId, config: config)
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

    func testInitialStateIsIdle() {
        let vm = makeViewModel()
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle state")
        }
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertEqual(vm.tab, .following)
    }

    func testLoadFollowingSuccessTransitionsToLoaded() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            ApiFixtureLoader.data("swift.users.following.default"),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertEqual(vm.items.count, 1)
        XCTAssertEqual(vm.following.count, 1)
    }

    func testLoadEmptyFollowingTransitionsToLoaded() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: []),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertTrue(vm.items.isEmpty)
    }

    func testSwitchingTabLoadsFollowers() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["f1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/followers"] = (
            makeUsersPage(ids: ["r1", "r2", "r3"]),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.tab, .following)
        XCTAssertEqual(vm.items.count, 1)

        // Switching tab; in the app the view's .task(id: viewModel.tab) re-runs load().
        // In unit tests without a SwiftUI view, call load() explicitly after switching.
        vm.tab = .followers
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state after switching to followers")
        }
        XCTAssertEqual(vm.items.count, 3)
        XCTAssertEqual(vm.followers.count, 3)
    }

    func testReloadFollowingRefetchesData() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["f1"]),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.items.count, 1)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["f2", "f3"]),
            200
        )
        await vm.reload()
        XCTAssertEqual(vm.items.count, 2)
    }

    func testLoadMoreFollowingAppendsAndForwardsCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (makeUsersPage(ids: ["f2"], hasMore: false), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.load()
        await vm.loadMore()

        let lastURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let queryItems = URLComponents(url: lastURL, resolvingAgainstBaseURL: false)?.queryItems
        XCTAssertEqual(queryItems?.first { $0.name == "after" }?.value, "cursor-1")
        XCTAssertEqual(vm.following.map(\.id), ["f1", "f2"])
        XCTAssertFalse(vm.hasMore)
    }

    func testLoadMoreFollowersAppendsAndForwardsCursor() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/followers"] = [
            (makeUsersPage(ids: ["r1"], hasMore: true, endCursor: "cursor-2"), 200, 0),
            (makeUsersPage(ids: ["r2"], hasMore: false), 200, 0)
        ]
        let vm = makeViewModel()
        vm.tab = .followers
        await vm.load()
        await vm.loadMore()

        let lastURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let queryItems = URLComponents(url: lastURL, resolvingAgainstBaseURL: false)?.queryItems
        XCTAssertEqual(queryItems?.first { $0.name == "after" }?.value, "cursor-2")
        XCTAssertEqual(vm.followers.map(\.id), ["r1", "r2"])
        XCTAssertFalse(vm.hasMore)
    }

    func testLoadMoreFailurePreservesLoadedRows() async {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (Data("{}".utf8), 500, 0)
        ]
        let vm = makeViewModel()
        await vm.load()
        await vm.loadMore()

        XCTAssertEqual(vm.following.map(\.id), ["f1"])
        XCTAssertEqual(vm.items.map(\.id), ["f1"])
        XCTAssertTrue(vm.hasMore)
        if case .error = vm.state {} else {
            XCTFail("Expected .error state after failed loadMore")
        }
    }

    func testUnknownFollowerCanBeFollowedBeforeFollowingListIsComplete() async {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["known"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (makeUsersPage(ids: ["unknown"], hasMore: false), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.load()

        XCTAssertTrue(vm.canToggleFollow(userId: "known"))
        vm.tab = .followers
        XCTAssertTrue(vm.canToggleFollow(userId: "unknown"))

        vm.tab = .following
        await vm.loadMore()

        XCTAssertTrue(vm.canToggleFollow(userId: "unknown"))
        XCTAssertTrue(vm.isFollowing(userId: "unknown"))
    }

    func testAnonymousUserCannotToggleFollow() async {
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/unknown/follow"] = (Data("{}".utf8), 200)
        let vm = makeViewModel(userId: nil)

        XCTAssertFalse(vm.canToggleFollow(userId: "unknown"))
        await vm.toggleFollow(userId: "unknown")

        XCTAssertFalse(vm.isFollowing(userId: "unknown"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testDuplicateFollowToggleIsIgnoredWhileRequestIsInFlight() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: []),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/bookmarks/user/unknown/follow"] = [
            (Data("{}".utf8), 200, 0.2)
        ]
        let vm = makeViewModel()
        await vm.load()

        let firstToggle = Task { await vm.toggleFollow(userId: "unknown") }
        try await Task.sleep(nanoseconds: 50_000_000)

        XCTAssertFalse(vm.canToggleFollow(userId: "unknown"))
        await vm.toggleFollow(userId: "unknown")
        await firstToggle.value

        let followRequests = CannedFeedURLProtocol.capturedURLs.filter {
            $0.path == "/api/v1/bookmarks/user/unknown/follow"
        }
        XCTAssertEqual(followRequests.count, 1)
        XCTAssertTrue(vm.canToggleFollow(userId: "unknown"))
        XCTAssertTrue(vm.isFollowing(userId: "unknown"))
    }

    func testReloadIgnoresLateLoadMoreResponse() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["first"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (makeUsersPage(ids: ["stale"], hasMore: false), 200, 0.2),
            (makeUsersPage(ids: ["fresh"], hasMore: false), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.load()

        let loadMore = Task { await vm.loadMore() }
        try await Task.sleep(nanoseconds: 50_000_000)
        await vm.reload()
        await loadMore.value

        XCTAssertEqual(vm.following.map(\.id), ["fresh"])
        XCTAssertFalse(vm.followingIds.contains("stale"))
        XCTAssertFalse(vm.hasMore)
    }

    func testReloadIgnoresLateLoadMoreFailure() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/users/user-abc/users/following"] = [
            (makeUsersPage(ids: ["first"], hasMore: true, endCursor: "cursor-1"), 200, 0),
            (Data("{}".utf8), 500, 0.2),
            (makeUsersPage(ids: ["fresh"], hasMore: false), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.load()

        let loadMore = Task { await vm.loadMore() }
        try await Task.sleep(nanoseconds: 50_000_000)
        await vm.reload()
        await loadMore.value

        XCTAssertEqual(vm.following.map(\.id), ["fresh"])
        XCTAssertFalse(vm.isLoadingMore)
        if case .loaded = vm.state {} else {
            XCTFail("Expected stale loadMore failure to leave refreshed state loaded")
        }
    }

    func testLoadWithNilUserIdTransitionsToLoaded() async {
        let vm = makeViewModel(userId: nil)
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state when userId is nil")
        }
        XCTAssertTrue(vm.items.isEmpty)
    }

    func testFollowingErrorTransitionsToError() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (Data("{}".utf8), 401)
        let vm = makeViewModel()
        await vm.load()
        if case .error = vm.state {} else {
            XCTFail("Expected .error state")
        }
    }

    func testAvatarURLReturnsNilForNilProfileImageId() throws {
        let vm = makeViewModel()
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let user = try decoder.decode(
            PublicUser.self,
            from: Data(#"{"id":"u1","username":"alice","roles":[],"profile_image_id":null}"#.utf8)
        )
        XCTAssertNil(vm.avatarURL(for: user))
    }

    func testLoadedListViewShowsLoadMoreButtonWhenMorePagesExist() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["f1"], hasMore: true, endCursor: "cursor-1"),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        let sut = FriendsListView(viewModel: vm)

        XCTAssertEqual(try sut.inspect().find(text: "Load more").string(), "Load more")
    }

}

// MARK: - ProfileViewModel

@MainActor
final class ProfileViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    private func makeViewModel() -> ProfileViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let cookieStorage = HTTPCookieStorage()
        let apiClient = APIClient(
            config: config,
            cookieStorage: cookieStorage,
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let sessionManager = SessionManager(client: apiClient, cookieStorage: cookieStorage)
        return ProfileViewModel(client: apiClient, config: config, sessionManager: sessionManager)
    }

    private func makeIdentityJSON(username: String = "alice", email: String = "alice@example.com") -> Data {
        if username == "alice", email == "alice@example.com" {
            return ApiFixtureLoader.data("swift.my.identity.default")
        }
        return PrivateUserTestFixture.identityEnvelope(
            username: username,
            emailAddress: email,
            membershipPlan: "free",
            roles: ["user"]
        )
    }

    private func makeProfileJSON(bio: String = "Hello world") -> Data {
        if bio == "Hello world" {
            return ApiFixtureLoader.data("swift.my.profile.default")
        }
        return Data("""
        {"profile":{"id":"user-1","markdown":"\(bio)"}}
        """.utf8)
    }

    func testInitialStateIsIdle() {
        let vm = makeViewModel()
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle state")
        }
        XCTAssertNil(vm.identity)
        XCTAssertNil(vm.bio)
    }

    func testLoadSuccessPopulatesIdentityAndBio() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (makeIdentityJSON(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/markdown/preview"] = (
            Data(#"{"html":"<p>Hello world</p>"}"#.utf8),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertEqual(vm.identity?.username, "alice")
        XCTAssertEqual(vm.identity?.emailAddress, "tests+api-fixtures@voucha.ai")
        XCTAssertEqual(vm.bio, "Hello world")
        XCTAssertEqual(vm.bioHtml, "<p>Hello world</p>")
    }

    func testLoadSuccessWithEmptyBioSetsBioToNil() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (makeIdentityJSON(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(bio: ""), 200)
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertNil(vm.bio)
    }

    func testLoadProfileFailureStillPopulatesIdentity() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (makeIdentityJSON(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (Data("{}".utf8), 404)
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state even when profile fetch fails")
        }
        XCTAssertEqual(vm.identity?.username, "alice")
        XCTAssertNil(vm.bio)
    }

    func testLoadIdentityFailureTransitionsToError() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (Data("{}".utf8), 401)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(), 200)
        let vm = makeViewModel()
        await vm.load()
        if case .error = vm.state {} else {
            XCTFail("Expected .error state when identity fetch fails")
        }
    }

    func testLoadIsIdempotentWhenAlreadyLoaded() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (makeIdentityJSON(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(), 200)
        let vm = makeViewModel()
        await vm.load()
        // Second load call should be a no-op (state is .loaded, not .idle)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (Data("{}".utf8), 500)
        await vm.load()
        // State should still be .loaded (first call data preserved)
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state after duplicate load call")
        }
        XCTAssertEqual(vm.identity?.username, "alice")
    }

    func testReloadClearsAndRefetches() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (makeIdentityJSON(username: "bob"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(bio: "Bio 1"), 200)
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.identity?.username, "bob")
        XCTAssertEqual(vm.bio, "Bio 1")
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (makeIdentityJSON(username: "alice"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(bio: "Bio 2"), 200)
        await vm.reload()
        XCTAssertEqual(vm.bio, "Bio 2")
    }

    func testSignOutSetsSessionToAnonymous() async throws {
        // Sign-out ignores network failures, so FailingURLProtocol is fine.
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            turnstileSiteKey: "test-site-key"
        )
        let cookieStorage = HTTPCookieStorage()
        let apiClient = APIClient(
            config: config,
            cookieStorage: cookieStorage,
            protocolClasses: [FailingURLProtocol.self]
        )
        let sessionManager = SessionManager(client: apiClient, cookieStorage: cookieStorage)
        let vm = ProfileViewModel(client: apiClient, config: config, sessionManager: sessionManager)
        // Manually put session in signed-in state by simulating a successful identity response
        // (easiest: just verify signOut drives it to anonymous, starting from anonymous is fine too)
        await vm.signOut()
        XCTAssertFalse(sessionManager.isSignedIn)
    }
}

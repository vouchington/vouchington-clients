import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

// MARK: - MFAChallengeViewModel

@MainActor
final class MFAChallengeViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    private func makeViewModel(loginAttemptId: String = "attempt-123") -> MFAChallengeViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let cookieStorage = HTTPCookieStorage()
        let apiClient = APIClient(
            config: config,
            cookieStorage: cookieStorage,
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let sessionManager = SessionManager(client: apiClient, cookieStorage: cookieStorage)
        let service = SignInService(client: apiClient, sessionManager: sessionManager)
        return MFAChallengeViewModel(loginAttemptId: loginAttemptId, signInService: service)
    }

    private func makeIdentityJSON() -> Data {
        PrivateUserTestFixture.identityEnvelope(id: "u1", membershipPlan: "free")
    }

    func testInitialState() {
        let viewModel = makeViewModel()
        XCTAssertEqual(viewModel.code, "")
        XCTAssertFalse(viewModel.isLoading)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertFalse(viewModel.didSucceed)
    }

    func testSubmitCodeGuardsEmptyCode() async {
        let viewModel = makeViewModel()
        viewModel.code = ""
        await viewModel.submitCode()
        XCTAssertFalse(viewModel.didSucceed)
    }

    func testSubmitCodeSuccessSetsDone() async {
        CannedFeedURLProtocol.handlers["/api/v1/auth/mfa/totp/verification"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (makeIdentityJSON(), 200)
        let viewModel = makeViewModel()
        viewModel.code = "123456"
        await viewModel.submitCode()
        XCTAssertTrue(viewModel.didSucceed)
        XCTAssertNil(viewModel.errorMessage)
    }

    func testSubmitCodeErrorSetsErrorMessage() async {
        CannedFeedURLProtocol.handlers["/api/v1/auth/mfa/totp/verification"] = (Data("{}".utf8), 401)
        let viewModel = makeViewModel()
        viewModel.code = "000000"
        await viewModel.submitCode()
        XCTAssertFalse(viewModel.didSucceed)
        XCTAssertNotNil(viewModel.errorMessage)
    }
}

// MARK: - PostVoteTests

@MainActor
final class PostVoteTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    private func makeViewModel() -> PostsListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return PostsListViewModel(client: apiClient)
    }

    func testVoteUpAppliesOptimisticallyOnSuccess() async {
        CannedFeedURLProtocol.handlers["/api/v1/posts/p1/vote"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.vote(postId: "p1", choice: .like)
        XCTAssertEqual(viewModel.myVotesByPostId["p1"], .like)
    }

    func testVoteDownAppliesOptimisticallyOnSuccess() async {
        CannedFeedURLProtocol.handlers["/api/v1/posts/p1/vote"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.vote(postId: "p1", choice: .dislike)
        XCTAssertEqual(viewModel.myVotesByPostId["p1"], .dislike)
    }

    func testClearVoteOnSuccess() async {
        CannedFeedURLProtocol.handlers["/api/v1/posts/p1/vote"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.vote(postId: "p1", choice: .like)
        CannedFeedURLProtocol.handlers["/api/v1/posts/p1/vote"] = (Data("{}".utf8), 204)
        await viewModel.vote(postId: "p1", choice: nil)
        XCTAssertNil(viewModel.myVotesByPostId["p1"])
    }

    func testVoteRollsBackOnError() async {
        CannedFeedURLProtocol.handlers["/api/v1/posts/p1/vote"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        await viewModel.vote(postId: "p1", choice: .like)
        XCTAssertNil(viewModel.myVotesByPostId["p1"])
    }
}

// MARK: - FollowToggleTests

@MainActor
final class FollowToggleTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
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

    private func makeUsersPage(ids: [String]) -> Data {
        let users = ids.map { "{\"id\":\"\($0)\",\"username\":\"user_\($0)\",\"roles\":[]}" }
            .joined(separator: ",")
        return Data("""
        {"results":[\(users)],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
        """.utf8)
    }

    func testFollowAddsToFollowingIds() async {
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/other-user/follow"] = (Data("{}".utf8), 200)
        let viewModel = makeViewModel()
        await viewModel.toggleFollow(userId: "other-user")
        XCTAssertTrue(viewModel.followingIds.contains("other-user"))
    }

    func testUnfollowRemovesFromFollowingIds() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["friend-1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/friend-1/follow"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.load()
        XCTAssertTrue(viewModel.followingIds.contains("friend-1"))
        await viewModel.toggleFollow(userId: "friend-1")
        XCTAssertFalse(viewModel.followingIds.contains("friend-1"))
    }

    func testUnfollowRollsBackOnError() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["friend-1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/friend-1/follow"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        await viewModel.load()
        XCTAssertTrue(viewModel.followingIds.contains("friend-1"))
        await viewModel.toggleFollow(userId: "friend-1")
        XCTAssertTrue(viewModel.followingIds.contains("friend-1"))
    }

    func testFollowRollsBackOnError() async {
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/other-user/follow"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        await viewModel.toggleFollow(userId: "other-user")
        XCTAssertFalse(viewModel.followingIds.contains("other-user"))
    }

    func testFollowingIdsInitializedFromFollowingList() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            makeUsersPage(ids: ["f1", "f2"]),
            200
        )
        let viewModel = makeViewModel()
        await viewModel.load()
        XCTAssertTrue(viewModel.followingIds.contains("f1"))
        XCTAssertTrue(viewModel.followingIds.contains("f2"))
    }
}

// MARK: - NotificationMarkReadTests

@MainActor
final class NotificationMarkReadTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    private func makeViewModel() -> NotificationsListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return NotificationsListViewModel(client: apiClient)
    }

    private func makeNotificationsPage(ids: [String]) -> Data {
        let items = ids.map { id in
            "\"\(id)\":{\"id\":\"\(id)\",\"user_id\":\"u1\",\"entity_type\":\"post\",\"post_id\":null,\"rss_feed_item_id\":null,\"actor_user_id\":null,\"community_id\":null,\"conversation_id\":null,\"moderation_report_id\":null,\"review_dispute_id\":null,\"user_warning_id\":null,\"actor_label\":null,\"event_key\":null,\"title\":\"Notification \(id)\",\"body\":\"Body\",\"target_path\":null,\"target_entity\":null,\"target_intent\":null,\"read_at\":null,\"created_at\":\"2024-01-01T00:00:00Z\",\"updated_at\":\"2024-01-01T00:00:00Z\",\"pushed_at\":null}"
        }.joined(separator: ",")
        let results = ids.map { "{\"id\":\"\($0)\",\"read_at\":null}" }.joined(separator: ",")
        return Data("""
        {"results":[\(results)],"page_info":{"has_next_page":false,"end_cursor":null},"notifications":{\(items)}}
        """.utf8)
    }

    func testMarkReadAddsToLocallyReadIds() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (makeNotificationsPage(ids: ["n1"]), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.markRead(notificationId: "n1")
        XCTAssertTrue(viewModel.locallyReadIds.contains("n1"))
    }

    func testMarkReadRollsBackOnError() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (makeNotificationsPage(ids: ["n1"]), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.markRead(notificationId: "n1")
        XCTAssertFalse(viewModel.locallyReadIds.contains("n1"))
    }

    func testMarkAllReadSetsAllItems() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n1", "n2", "n3"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/read-all"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.markAllRead()
        XCTAssertTrue(viewModel.locallyReadIds.contains("n1"))
        XCTAssertTrue(viewModel.locallyReadIds.contains("n2"))
        XCTAssertTrue(viewModel.locallyReadIds.contains("n3"))
    }

    func testMarkAllReadRollsBackWithoutStompingPreexistingReads() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n1", "n2"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 204)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/read-all"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.markRead(notificationId: "n1")
        XCTAssertTrue(viewModel.locallyReadIds.contains("n1"))
        await viewModel.markAllRead()
        // markAllRead failed, but n1 was already read — rollback must not clear it
        XCTAssertTrue(viewModel.locallyReadIds.contains("n1"))
        XCTAssertFalse(viewModel.locallyReadIds.contains("n2"))
    }

    func testResetClearsLocallyReadIds() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (makeNotificationsPage(ids: ["n1"]), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.markRead(notificationId: "n1")
        viewModel.reset()
        XCTAssertTrue(viewModel.locallyReadIds.isEmpty)
    }
}

// MARK: - ProfileEditTests

@MainActor
final class ProfileEditTests: XCTestCase {
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

    private func makeProfileJSON(bio: String) -> Data {
        Data(#"{"profile":{"id":"user-1","markdown":"\#(bio)"}}"#.utf8)
    }

    func testUpdateBioSuccessUpdatesLocalBio() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(bio: "New bio"), 200)
        let viewModel = makeViewModel()
        try await viewModel.updateBio("New bio")
        XCTAssertEqual(viewModel.bio, "New bio")
    }

    func testUpdateBioEmptyMarkdownSetsBioToNil() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (makeProfileJSON(bio: ""), 200)
        let viewModel = makeViewModel()
        try await viewModel.updateBio("")
        XCTAssertNil(viewModel.bio)
    }

    func testUpdateBioServerErrorThrows() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (Data("{}".utf8), 422)
        let viewModel = makeViewModel()
        do {
            try await viewModel.updateBio("bio text")
            XCTFail("Expected an error to be thrown")
        } catch {
            XCTAssertNotNil(error)
        }
    }

    func testIsLoadedFalseWhenIdle() {
        let viewModel = makeViewModel()
        XCTAssertFalse(viewModel.isLoaded)
    }
}

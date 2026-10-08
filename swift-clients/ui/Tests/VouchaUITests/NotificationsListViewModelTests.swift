import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

// MARK: - NotificationsListViewModel

@MainActor
final class NotificationsListViewModelTests: XCTestCase {
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

    private func makeNotificationsPage(
        ids: [String],
        hasMore: Bool,
        endCursor: String? = nil,
        readAt: String? = nil
    ) -> Data {
        let items = ids.map { id in
            "\"\(id)\":{\"id\":\"\(id)\",\"user_id\":\"u1\",\"entity_type\":\"post\",\"post_id\":null,\"rss_feed_item_id\":null,\"actor_user_id\":null,\"community_id\":null,\"conversation_id\":null,\"moderation_report_id\":null,\"review_dispute_id\":null,\"user_warning_id\":null,\"copyright_notice_id\":null,\"actor_label\":null,\"event_key\":null,\"title\":\"Notification \(id)\",\"body\":\"Body \(id)\",\"target_path\":null,\"target_entity\":null,\"target_intent\":null,\"read_at\":\(readAt.map { "\"\($0)\"" } ?? "null"),\"created_at\":\"2024-01-01T00:00:00Z\",\"updated_at\":\"2024-01-01T00:00:00Z\",\"pushed_at\":null}"
        }.joined(separator: ",")
        let results = ids.map { "{\"id\":\"\($0)\",\"read_at\":null}" }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {"results":[\(results)],"page_info":{"has_next_page":\(hasMore),"end_cursor":\(cursor)},"notifications":{\(
            items
        )}}
        """.utf8)
    }

    func testInitialState() {
        let vm = makeViewModel()
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertFalse(vm.hasMore)
        XCTAssertFalse(vm.isLoading)
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle state")
        }
    }

    func testLoadSuccessTransitionsToLoaded() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            ApiFixtureLoader.data("swift.notifications.default"),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertEqual(
            vm.items.map(\.entityType),
            [.post, .communityApplicationDecision, .communityActivityDigest]
        )
        XCTAssertFalse(vm.hasMore)
    }

    func testLoadEmptyResultsTransitionsToLoaded() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: [], hasMore: false),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertTrue(vm.items.isEmpty)
    }

    func testPaginationAccumulatesItems() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n1", "n2"], hasMore: true, endCursor: "c2"),
            200
        )
        let vm = makeViewModel()
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 2)
        XCTAssertTrue(vm.hasMore)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n3"], hasMore: false),
            200
        )
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 3)
        XCTAssertFalse(vm.hasMore)
    }

    func testCursorForwardedInSecondRequest() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n1"], hasMore: true, endCursor: "cursorA"),
            200
        )
        let vm = makeViewModel()
        await vm.loadNextPage()
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n2"], hasMore: false),
            200
        )
        await vm.loadNextPage()
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(
            components?.queryItems?.first(where: { $0.name == "after" })?.value,
            "cursorA"
        )
    }

    func testUnauthorizedErrorTransitionsToVouchaError() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (Data("{}".utf8), 401)
        let vm = makeViewModel()
        await vm.load()
        if case let .error(err) = vm.state, case .unauthorized = err {} else {
            XCTFail("Expected .error(.unauthorized) state")
        }
    }

    func testReloadClearsAndRefetches() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n1"], hasMore: false),
            200
        )
        let vm = makeViewModel()
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 1)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n2", "n3"], hasMore: false),
            200
        )
        await vm.reload()
        XCTAssertEqual(vm.items.count, 2)
    }

    func testResetClearsState() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n1"], hasMore: false),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        vm.reset()
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertFalse(vm.hasMore)
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle after reset")
        }
    }

    func testPaginationErrorPreservesExistingItems() async {
        // Regression: when a subsequent page fetch fails, existing items must be preserved
        // and state must be .error so the view can show an inline retry footer.
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(ids: ["n1", "n2"], hasMore: true, endCursor: "c2"),
            200
        )
        let vm = makeViewModel()
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 2)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (Data("{}".utf8), 500)
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 2, "Existing items must survive a failed page fetch")
        if case .error = vm.state {} else {
            XCTFail("Expected .error after failed page fetch")
        }
    }
}

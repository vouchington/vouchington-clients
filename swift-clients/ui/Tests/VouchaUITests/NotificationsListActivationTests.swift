import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NotificationsListActivationTests: XCTestCase {
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

    private func makeNotification(
        id: String,
        targetPath: String?,
        readAt: Date? = nil
    ) -> VouchaNotification {
        let targetPathJSON = targetPath.map { "\"\($0)\"" } ?? "null"
        let readAtJSON = readAt.map { "\"\(ISO8601DateFormatter().string(from: $0))\"" } ?? "null"
        let json = Data("""
        {
          "id":"\(id)",
          "user_id":"u1",
          "entity_type":"post",
          "post_id":"p1",
          "rss_feed_item_id":null,
          "actor_user_id":"u2",
          "community_id":null,
          "conversation_id":null,
          "moderation_report_id":null,
          "review_dispute_id":null,
          "user_warning_id":null,
          "actor_label":null,
          "event_key":null,
          "title":"Notification \(id)",
          "body":"Body \(id)",
          "target_path":\(targetPathJSON),
          "target_entity":null,
          "target_intent":null,
          "read_at":\(readAtJSON),
          "created_at":"2024-01-01T00:00:00Z",
          "updated_at":"2024-01-01T00:00:00Z",
          "pushed_at":null
        }
        """.utf8)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try! decoder.decode(VouchaNotification.self, from: json)
    }

    private func makeNotificationsPage(notification: VouchaNotification) -> Data {
        let targetPathJSON = notification.targetPath.map { "\"\($0)\"" } ?? "null"
        let readAtJSON = notification.readAt.map { "\"\(ISO8601DateFormatter().string(from: $0))\"" } ?? "null"
        return Data("""
        {
          "results":[{"id":"\(notification.id)","read_at":\(readAtJSON)}],
          "page_info":{"has_next_page":false,"end_cursor":null},
          "notifications":{
            "\(notification.id)":{
              "id":"\(notification.id)",
              "user_id":"\(notification.userId)",
              "entity_type":"post",
              "post_id":"\(notification.postId ?? "")",
              "rss_feed_item_id":null,
              "actor_user_id":"\(notification.actorUserId ?? "")",
              "community_id":null,
              "conversation_id":null,
              "moderation_report_id":null,
              "review_dispute_id":null,
              "user_warning_id":null,
              "actor_label":null,
              "event_key":null,
              "title":"\(notification.title)",
              "body":"\(notification.body)",
              "target_path":\(targetPathJSON),
              "target_entity":null,
              "target_intent":null,
              "read_at":\(readAtJSON),
              "created_at":"2024-01-01T00:00:00Z",
              "updated_at":"2024-01-01T00:00:00Z",
              "pushed_at":null
            }
          }
        }
        """.utf8)
    }

    private func decodeNotification(_ json: String) throws -> VouchaNotification {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(VouchaNotification.self, from: Data(json.utf8))
    }

    func testActivateReturnsTrimmedTargetPathAndMarksRead() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()
        let notification = makeNotification(id: "n1", targetPath: "  /posts/p1  ")

        let targetPath = await viewModel.activate(notification: notification)

        XCTAssertEqual(targetPath, "/posts/p1")
        XCTAssertTrue(viewModel.locallyReadIds.contains("n1"))
    }

    func testActivateReturnsNilForBlankOrNilTargetPath() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 204)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n2"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel()

        let blankTarget = await viewModel.activate(notification: makeNotification(id: "n1", targetPath: "   "))
        let nilTarget = await viewModel.activate(notification: makeNotification(id: "n2", targetPath: nil))

        XCTAssertNil(blankTarget)
        XCTAssertNil(nilTarget)
        XCTAssertTrue(viewModel.locallyReadIds.contains("n1"))
        XCTAssertTrue(viewModel.locallyReadIds.contains("n2"))
    }

    func testCommunityTargetFallsBackToNotificationsInboxWithoutSidecar() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n-community"] = (Data("{}".utf8), 204)
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let notification = try decoder.decode(VouchaNotification.self, from: Data(#"""
        {
          "id":"n-community","user_id":"u1","entity_type":"community_role_change",
          "community_id":null,"conversation_id":null,"moderation_report_id":null,
          "review_dispute_id":null,"user_warning_id":null,"actor_label":null,"event_key":null,
          "title":"Role changed","body":"Body","target_path":null,
          "target_entity":{"__entity_type":"community","id":"community-1"},
          "target_intent":null,"read_at":null,"created_at":"2024-01-01T00:00:00Z",
          "updated_at":"2024-01-01T00:00:00Z","pushed_at":null
        }
        """#.utf8))

        let targetPath = await makeViewModel().activate(notification: notification)

        XCTAssertEqual(targetPath, "/my/notifications")
    }

    func testCommunityTargetUsesHydratedCommunitySidecar() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            Data(
                #"{"results":[{"id":"n-community","read_at":"2024-01-01T00:00:00Z"}],"page_info":{"has_next_page":false,"end_cursor":null},"notifications":{"n-community":{"id":"n-community","user_id":"u1","entity_type":"community_role_change","community_id":null,"conversation_id":null,"moderation_report_id":null,"review_dispute_id":null,"user_warning_id":null,"actor_label":null,"event_key":null,"title":"Role changed","body":"Body","target_path":null,"target_entity":{"__entity_type":"community","id":"community-1"},"target_intent":null,"read_at":"2024-01-01T00:00:00Z","created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","pushed_at":null}},"communities":{"community-1":{"id":"community-1","slug":"builders","name":"Builders"}}}"#
                    .utf8
            ),
            200
        )
        let viewModel = makeViewModel()
        await viewModel.load()

        let targetPath = try await viewModel.activate(notification: XCTUnwrap(viewModel.items.first))

        XCTAssertEqual(targetPath, "/communities/builders")
    }

    func testNotificationsInboxIntentRoutesToInbox() async throws {
        let notification =
            try decodeNotification(
                #"{"id":"n-digest","user_id":"u1","entity_type":"community_activity_digest","community_id":null,"conversation_id":null,"moderation_report_id":null,"review_dispute_id":null,"user_warning_id":null,"actor_label":null,"event_key":null,"title":"Digest","body":"Body","target_path":null,"target_entity":null,"target_intent":"notifications_inbox","read_at":"2024-01-01T00:00:00Z","created_at":"2024-01-01T00:00:00Z","updated_at":"2024-01-01T00:00:00Z","pushed_at":null}"#
            )

        let targetPath = await makeViewModel().activate(notification: notification)

        XCTAssertEqual(targetPath, "/my/notifications")
    }

    func testActivateRollsBackAndReturnsNilWhenMarkReadFails() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()

        let targetPath = await viewModel.activate(notification: makeNotification(id: "n1", targetPath: "/posts/p1"))

        XCTAssertNil(targetPath)
        XCTAssertFalse(viewModel.locallyReadIds.contains("n1"))
    }

    func testActivateReturnsTargetPathWhenAlreadyReadNotificationMarkReadFails() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 500)
        let viewModel = makeViewModel()
        let notification = makeNotification(
            id: "n1",
            targetPath: " /posts/p1 ",
            readAt: Date(timeIntervalSince1970: 0)
        )

        let targetPath = await viewModel.activate(notification: notification)

        XCTAssertEqual(targetPath, "/posts/p1")
        XCTAssertFalse(viewModel.locallyReadIds.contains("n1"))
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/my/notifications/n1" })
    }

    func testActivateResolvesNotificationRedirectTargetPath() async {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 204)
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1/redirect-target"] = (
            Data(#"{"target_url":"/rss-feed-items/item-1"}"#.utf8),
            200
        )
        let viewModel = makeViewModel()
        let notification = makeNotification(
            id: "n1",
            targetPath: "/notification-redirect?notification_id=n1"
        )

        let targetPath = await viewModel.activate(notification: notification)

        XCTAssertEqual(targetPath, "/rss-feed-items/item-1")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/my/notifications/n1/redirect-target"
        })
    }

    func testNotificationRowTapInvokesNavigationAfterActivation() async throws {
        let notification = makeNotification(id: "n1", targetPath: " /posts/p1 ")
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            makeNotificationsPage(notification: notification),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications/n1"] = (Data("{}".utf8), 204)

        let viewModel = makeViewModel()
        await viewModel.load()

        var navigatedPath: String?
        let sut = NotificationsListView(
            viewModel: viewModel,
            onNavigateToTargetPath: { navigatedPath = $0 }
        )

        try sut.inspect().find(ViewType.View<NotificationCard>.self).callOnTapGesture()

        for _ in 0 ..< 20 {
            if navigatedPath == "/posts/p1" {
                break
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertEqual(navigatedPath, "/posts/p1")
    }

    func testInboxSettingsActionRoutesToNotificationSettings() throws {
        let viewModel = makeViewModel()
        var navigatedPath: String?
        let sut = NotificationsListView(
            viewModel: viewModel,
            onNavigateToTargetPath: { navigatedPath = $0 }
        )

        try sut.inspect().find(button: "Settings").tap()

        XCTAssertEqual(navigatedPath, "/my/notification-settings")
    }
}

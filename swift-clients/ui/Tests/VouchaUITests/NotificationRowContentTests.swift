import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NotificationRowContentTests: XCTestCase {
    private typealias Support = NotificationTextTestSupport

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    private func makeViewModel() -> NotificationsListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return NotificationsListViewModel(client: client)
    }

    func testViewModelRendersFixtureRowsFromHydrationMaps() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (
            ApiFixtureLoader.data("swift.notifications.default"),
            200
        )
        let viewModel = makeViewModel()
        await viewModel.load()
        let rendered = viewModel.items.map { notification -> RenderedNotification in
            let content = viewModel.content(for: notification)
            return RenderedNotification(title: uiEnglish(content.title), body: content.body.map(uiEnglish))
        }
        XCTAssertEqual(rendered.count, 3)
        XCTAssertEqual(rendered[0], RenderedNotification(title: "New discussion from @testuser", body: "Fixture post"))
        XCTAssertEqual(rendered[1].title, "Your application to Test Community was approved")
        XCTAssertEqual(rendered[1].body, "You are now a member of Test Community.")
        XCTAssertEqual(rendered[2].title, "Your weekly community activity")
        XCTAssertTrue(try XCTUnwrap(rendered[2].body).hasPrefix("1 community: 2 joined, 0 departed (net 2)"))
    }

    func testEachRowRendersFromItsOwnPageHydrationAndResetClearsIt() async {
        let path = "/api/v1/my/notifications"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (
                Support.pageJSON(
                    kind: "follow",
                    id: "n-first",
                    hasNext: true,
                    hydration: ["actors": Support.actors("u2", "alice")]
                ),
                200,
                0
            ),
            (Support.pageJSON(kind: "follow", id: "n-later"), 200, 0)
        ]
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.loadNextPage()
        XCTAssertEqual(viewModel.items.map(\.id), ["n-first", "n-later"])
        let first = viewModel.items[0]
        XCTAssertEqual(uiEnglish(viewModel.content(for: first).title), "@alice started following you")
        // The later page references u2 but omits it from `actors`, so that actor is gone for that row.
        XCTAssertEqual(uiEnglish(viewModel.content(for: viewModel.items[1]).title), "Someone started following you")
        viewModel.reset()
        XCTAssertEqual(uiEnglish(viewModel.content(for: first).title), "Someone started following you")
    }

    func testCardShowsTitleAndBodyForRowsWithBody() throws {
        let notification = try Support.notification("report_reviewed")
        let content = NotificationTextRenderer.render(
            notification,
            hydration: NotificationHydration(),
            webOrigin: Support.webOrigin
        )
        let card = NotificationCard(notification: notification, title: content.title, detail: content.body)
        let inspected = try card.inspect()
        XCTAssertNoThrow(try inspected.find(text: "Your report was reviewed"))
        XCTAssertNoThrow(try inspected.find(text: "Thanks for helping keep Voucha safe."))
    }

    func testCardOmitsBodyForTitleOnlyRows() throws {
        let notification = try Support.notification("modmail")
        let content = NotificationTextRenderer.render(
            notification,
            hydration: NotificationHydration(),
            webOrigin: Support.webOrigin
        )
        XCTAssertNil(content.body)
        let card = NotificationCard(
            notification: notification,
            title: content.title,
            detail: content.body,
            isRead: true
        )
        let inspected = try card.inspect()
        XCTAssertNoThrow(try inspected.find(text: "New modmail message"))
        XCTAssertEqual(try inspected.findAll(ViewType.Text.self).count, 2)
    }
}

import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PostsListBoundedWindowTests: XCTestCase {
    private let path = "/api/v1/feeds/posts/any"

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    override func tearDown() {
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    private func makeViewModel() -> PostsListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let client = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return PostsListViewModel(client: client)
    }

    private func page(_ ids: [String], hasNext: Bool, cursor: String?) -> Data {
        let posts = ids.map { id in
            "\"\(id)\":{\"id\":\"\(id)\",\"slug\":null,\"post_type\":\"discussion\",\"title\":\"Post \(id)\",\"markdown\":null,\"html\":null,\"parent_post_id\":null,\"root_post_id\":null,\"created_by_id\":\"user1\",\"created_at\":\"2024-01-01T00:00:00Z\",\"broadcast\":null,\"privacy\":\"public\",\"is_anonymous\":false,\"community_id\":null,\"clearance_status\":null,\"deleted_at\":null,\"deleted_by_id\":null,\"locked_at\":null,\"locked_by_id\":null,\"archived_at\":null,\"archived_by_id\":null,\"clearance_reason\":null,\"clearance_updated_at\":null,\"spam_detection_created_at\":null,\"spam_detection_flagged\":null,\"spam_detection_results\":null,\"spam_detection_score\":null,\"updated_by_id\":null}"
        }.joined(separator: ",")
        let results = ids.map { "{\"entity_id\":\"\($0)\"}" }.joined(separator: ",")
        let cursorJSON = cursor.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {"results":[\(results)],"page_info":{"has_next_page":\(hasNext),"end_cursor":\(cursorJSON)},\
        "posts":{\(posts)},"posts_metrics":{},"post_elections":{},"election_votes":{}}
        """.utf8)
    }

    private func queryItem(_ name: String, in url: URL?) -> String? {
        url.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?
            .queryItems?.first { $0.name == name }?.value
    }

    func testDefaultSendsExplicitMonthAndSelecting30DaysKeepsDefault() async {
        CannedFeedURLProtocol.handlers[path] = (page(["a"], hasNext: false, cursor: nil), 200)
        let vm = makeViewModel()
        await vm.load()
        await vm.selectTimeRange(.last30Days)
        await vm.selectTimeRange(.last30Days)

        let urls = CannedFeedURLProtocol.capturedURLs
        XCTAssertEqual(urls.count, 1)
        XCTAssertEqual(queryItem("time_range", in: urls[0]), "1m")
        XCTAssertNil(vm.timeRange)
    }

    func testFullPageContinuesWithOpaqueCursorAndShortPageIsTerminal() async {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (page(["a", "b"], hasNext: true, cursor: "opaque=="), 200, 0),
            (page(["c"], hasNext: false, cursor: "ignored"), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.selectTimeRange(.lastYear)
        XCTAssertTrue(vm.hasMore)
        await vm.loadNextPage()
        XCTAssertFalse(vm.hasMore)
        await vm.loadNextPage()

        let urls = CannedFeedURLProtocol.capturedURLs
        XCTAssertEqual(urls.count, 2)
        XCTAssertEqual(queryItem("after", in: urls[1]), "opaque==")
        XCTAssertEqual(queryItem("time_range", in: urls[1]), "1y")
        XCTAssertEqual(vm.items.map(\.id), ["a", "b", "c"])
    }

    func testChangingRangeDiscardsRowsCursorAndBookmarksThenDefaultSendsMonth() async {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (page(["old"], hasNext: true, cursor: "old-cursor"), 200, 0),
            (page(["year"], hasNext: true, cursor: "year-cursor"), 200, 0),
            (page(["month"], hasNext: false, cursor: nil), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.load()
        vm.savedPostIds = ["old"]
        vm.hiddenPostIds = ["old"]
        await vm.selectTimeRange(.lastYear)
        XCTAssertEqual(vm.items.map(\.id), ["year"])
        XCTAssertTrue(vm.savedPostIds.isEmpty)
        XCTAssertTrue(vm.hiddenPostIds.isEmpty)
        await vm.selectTimeRange(.last30Days)
        XCTAssertEqual(vm.items.map(\.id), ["month"])
        XCTAssertFalse(vm.hasMore)
        let urls = CannedFeedURLProtocol.capturedURLs
        XCTAssertEqual(urls.map { queryItem("after", in: $0) }, [nil, nil, nil])
        XCTAssertEqual(urls.map { queryItem("time_range", in: $0) }, ["1m", "1y", "1m"])
    }

    func testHasNextPageWithoutCursorIsTerminal() async {
        CannedFeedURLProtocol.handlers[path] = (page(["a"], hasNext: true, cursor: nil), 200)
        let vm = makeViewModel()
        await vm.load()
        XCTAssertFalse(vm.hasMore)
        await vm.loadNextPage()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
    }

    func testRankedWindowSplitAcrossPagesKeepsServerOrder() async {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (page(["z", "m"], hasNext: true, cursor: "c1"), 200, 0),
            (page(["a", "q"], hasNext: false, cursor: nil), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.load()
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.map(\.id), ["z", "m", "a", "q"])
    }

    func testResetWhileContinuationInFlightDropsStalePageAndCursor() async throws {
        CannedFeedURLProtocol.handlers[path] = (page(["a"], hasNext: true, cursor: "c1"), 200)
        let vm = makeViewModel()
        await vm.load()

        CannedFeedURLProtocol.handlers[path] = (page(["stale"], hasNext: true, cursor: "c2"), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let continuation = Task { await vm.loadNextPage() }
        do {
            _ = try await barrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await continuation.value
            throw error
        }
        vm.reset()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await continuation.value

        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertFalse(vm.hasMore)

        CannedFeedURLProtocol.handlers[path] = (page(["fresh"], hasNext: false, cursor: nil), 200)
        await vm.selectTimeRange(.lastYear)
        let last = CannedFeedURLProtocol.capturedURLs.last
        XCTAssertNil(queryItem("after", in: last))
        XCTAssertEqual(queryItem("time_range", in: last), "1y")
        XCTAssertEqual(vm.items.map(\.id), ["fresh"])
    }

    func testFailedContinuationRetriesWithSameCursorAndKeepsRows() async {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (page(["a"], hasNext: true, cursor: "c1"), 200, 0),
            (Data("{}".utf8), 500, 0),
            (page(["b"], hasNext: false, cursor: nil), 200, 0)
        ]
        let vm = makeViewModel()
        await vm.load()
        await vm.loadNextPage()
        XCTAssertTrue(vm.hasPaginationError)
        XCTAssertEqual(vm.items.map(\.id), ["a"])

        await vm.loadNextPage()
        let urls = CannedFeedURLProtocol.capturedURLs
        XCTAssertEqual(urls.map { queryItem("after", in: $0) }, [nil, "c1", "c1"])
        XCTAssertEqual(vm.items.map(\.id), ["a", "b"])
        XCTAssertFalse(vm.hasPaginationError)
    }
}

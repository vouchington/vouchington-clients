import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class ForwardPaginationMigrationTests: XCTestCase {
    func testForwardContinuationPreservesRowsAndCursorAcrossRetry() throws {
        var pagination = CursorPaginationState<TestPageItem>()
        let initial = try XCTUnwrap(pagination.beginNextPage())
        pagination.complete(
            initial,
            items: [.init(id: "one")],
            endCursor: "cursor-one",
            hasNextPage: true
        )

        let failed = try XCTUnwrap(pagination.beginNextPage())
        XCTAssertEqual(failed.cursor, "cursor-one")
        pagination.fail(failed, error: .api(statusCode: 503, preconditionCode: nil))

        XCTAssertEqual(pagination.items.map(\.id), ["one"])
        XCTAssertFalse(pagination.canAutomaticallyLoad)

        let retry = try XCTUnwrap(pagination.beginNextPage())
        XCTAssertEqual(retry.cursor, "cursor-one")
        pagination.complete(
            retry,
            items: [.init(id: "one"), .init(id: "two")],
            endCursor: nil,
            hasNextPage: false
        )
        XCTAssertEqual(pagination.items.map(\.id), ["one", "two"])
    }

    func testResetRejectsStaleForwardPage() throws {
        var pagination = CursorPaginationState<TestPageItem>()
        let stale = try XCTUnwrap(pagination.beginNextPage())
        pagination.reset(items: [.init(id: "refreshed")])

        XCTAssertFalse(pagination.complete(
            stale,
            items: [.init(id: "stale")],
            endCursor: nil,
            hasNextPage: false
        ))
        XCTAssertEqual(pagination.items.map(\.id), ["refreshed"])
    }

    func testReverseHistoryPrependRemainsAnExplicitPaginationPosition() throws {
        var pagination = CursorPaginationState(items: [TestPageItem(id: "newest")])
        let request = try XCTUnwrap(pagination.beginNextPage())
        pagination.complete(
            request,
            items: [.init(id: "oldest")],
            endCursor: nil,
            hasNextPage: false,
            position: .prepend
        )
        XCTAssertEqual(pagination.items.map(\.id), ["oldest", "newest"])
    }
}

private struct TestPageItem: Identifiable {
    let id: String
}

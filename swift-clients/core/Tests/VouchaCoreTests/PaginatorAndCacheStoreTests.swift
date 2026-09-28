import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaPersistence
import XCTest

// MARK: - Cursor pagination state

final class CursorPaginationStateTests: XCTestCase {
    func testEmptyStateDoesNotAdvertiseContinuationBeforeInitialPageStarts() {
        var state = CursorPaginationState<TestItem>()

        XCTAssertFalse(state.hasMore)
        XCTAssertNotNil(state.beginInitialPageIfNeeded())
    }

    func testAppendsUniqueItemsAndAdvancesCursor() throws {
        var state = CursorPaginationState<TestItem>()
        let request = try XCTUnwrap(state.beginNextPage())

        XCTAssertTrue(state.complete(
            request,
            items: [TestItem(id: "one"), TestItem(id: "one"), TestItem(id: "two")],
            endCursor: "cursor-2",
            hasNextPage: true
        ))
        XCTAssertEqual(state.items.map(\.id), ["one", "two"])
        XCTAssertEqual(state.endCursor, "cursor-2")
        XCTAssertTrue(state.hasMore)
    }

    func testRejectsConcurrentAndStaleRequestsAfterReset() throws {
        var state = CursorPaginationState<TestItem>()
        let staleRequest = try XCTUnwrap(state.beginNextPage())
        XCTAssertNil(state.beginNextPage())

        state.reset()

        XCTAssertFalse(state.complete(staleRequest, items: [TestItem(id: "stale")], endCursor: nil, hasNextPage: false))
        XCTAssertTrue(state.items.isEmpty)
    }

    func testFailurePreservesItemsAndDisablesAutomaticLoadingUntilRetrySucceeds() throws {
        var state = CursorPaginationState<TestItem>(items: [TestItem(id: "one")])
        let request = try XCTUnwrap(state.beginNextPage())
        XCTAssertTrue(state.fail(request, error: .api(statusCode: 500, preconditionCode: nil)))
        XCTAssertEqual(state.items.map(\.id), ["one"])
        XCTAssertFalse(state.canAutomaticallyLoad)

        let retry = try XCTUnwrap(state.beginNextPage())
        XCTAssertEqual(retry.cursor, request.cursor)
        XCTAssertFalse(state.complete(request, items: [TestItem(id: "stale")], endCursor: nil, hasNextPage: false))
        XCTAssertTrue(state.complete(retry, items: [TestItem(id: "two")], endCursor: nil, hasNextPage: false))
        XCTAssertFalse(state.hasMore)
    }

    func testCancellationPreservesItemsAndContinuationWithoutAnError() throws {
        var state = CursorPaginationState(items: [TestItem(id: "one")])
        state.restoreContinuation(endCursor: "cursor-1", hasMore: true)
        let request = try XCTUnwrap(state.beginNextPage())

        XCTAssertTrue(state.cancel(request))
        XCTAssertEqual(state.items.map(\.id), ["one"])
        XCTAssertEqual(state.endCursor, "cursor-1")
        XCTAssertTrue(state.hasMore)
        XCTAssertNil(state.lastError)
        XCTAssertFalse(state.isLoading)
    }

    func testPrependKeepsCallerSelectedOrderAndDeduplicates() throws {
        var state = CursorPaginationState<TestItem>(items: [TestItem(id: "two"), TestItem(id: "three")])
        let request = try XCTUnwrap(state.beginNextPage())
        XCTAssertTrue(state.complete(
            request,
            items: [TestItem(id: "one"), TestItem(id: "two")],
            endCursor: nil,
            hasNextPage: false,
            position: .prepend
        ))
        XCTAssertEqual(state.items.map(\.id), ["one", "two", "three"])
    }

    func testDeliveryIdentityRetainsSharedItemAcrossPages() throws {
        struct Delivery: Identifiable {
            let id: String
            let itemId: String
        }
        var state = CursorPaginationState<Delivery>()
        let first = try XCTUnwrap(state.beginNextPage())
        XCTAssertTrue(state.complete(
            first,
            items: [
                Delivery(id: "share-A", itemId: "item-X"),
                Delivery(id: "share-B", itemId: "item-X"),
                Delivery(id: "direct", itemId: "item-X")
            ],
            endCursor: "cursor-2",
            hasNextPage: true
        ))
        let second = try XCTUnwrap(state.beginNextPage())
        XCTAssertTrue(state.complete(
            second,
            items: [
                Delivery(id: "share-A", itemId: "item-X"),
                Delivery(id: "share-C", itemId: "item-X")
            ],
            endCursor: nil,
            hasNextPage: false
        ))
        XCTAssertEqual(state.items.map(\.id), ["share-A", "share-B", "direct", "share-C"])
        XCTAssertEqual(state.items.map(\.itemId), ["item-X", "item-X", "item-X", "item-X"])
    }
}

private struct TestItem: Identifiable {
    let id: String
}

// MARK: - MemoryDiskCacheStore

final class MemoryDiskCacheStoreTests: XCTestCase {
    private var cacheDir: URL!
    private var store: MemoryDiskCacheStore!

    override func setUp() {
        super.setUp()
        cacheDir = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try? FileManager.default.createDirectory(at: cacheDir, withIntermediateDirectories: true)
        store = MemoryDiskCacheStore(directory: cacheDir)
    }

    override func tearDown() {
        try? FileManager.default.removeItem(at: cacheDir)
        super.tearDown()
    }

    func testRoundTrip() async {
        await store.set(key: "foo", value: "hello world", ttl: nil)
        let result = await store.get(key: "foo", type: String.self)
        XCTAssertEqual(result, "hello world")
    }

    func testInvalidateRemovesEntry() async {
        await store.set(key: "bar", value: 99, ttl: nil)
        await store.invalidate(key: "bar")
        let result = await store.get(key: "bar", type: Int.self)
        XCTAssertNil(result)
    }

    func testExpiredEntryReturnsNil() async {
        await store.set(key: "exp", value: "expires soon", ttl: -1)
        let result = await store.get(key: "exp", type: String.self)
        XCTAssertNil(result)
    }

    func testInvalidateAll() async {
        await store.set(key: "k1", value: "v1", ttl: nil)
        await store.set(key: "k2", value: "v2", ttl: nil)
        await store.invalidateAll()
        let r1 = await store.get(key: "k1", type: String.self)
        let r2 = await store.get(key: "k2", type: String.self)
        XCTAssertNil(r1)
        XCTAssertNil(r2)
    }
}

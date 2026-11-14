import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationQueueReconciliationTests: NativeRouteSurfaceViewModelTestCase {
    func testDisputeDeliveryPreservesLoadedPagesAndCursor() async throws {
        let path = "/api/v1/disputes"
        try registerPages(path: path, fixture: "native.moderation.disputes", key: "disputes")
        let model = try ReviewDisputesViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        await model.load()
        await model.loadMore()
        let ids = model.summaryPagination.items.map(\.id)
        XCTAssertEqual(ids.count, 2)
        let id = try XCTUnwrap(ids.first)
        CannedFeedURLProtocol.handlers[path + "/" + id] = try (
            detail(fixture: "native.moderation.disputes.detail.default", key: "dispute", sent: false), 200
        )
        await model.loadDetail(id: id)
        let initial = try XCTUnwrap(model.detailReviewDispute)
        XCTAssertNil(model.summaryPagination.items.first?.publicResponse)
        XCTAssertTrue(try XCTUnwrap(model.summaryPagination.items.first).hasDraft)
        CannedFeedURLProtocol.handlers[path + "/" + id + "/delivery"] = try (
            detail(fixture: "native.moderation.disputes.detail.default", key: "dispute", sent: true), 200
        )
        await model.deliver(initial)
        XCTAssertEqual(model.summaryPagination.items.map(\.id), ids)
        XCTAssertEqual(model.summaryPagination.endCursor, "retained-cursor")
        XCTAssertTrue(model.summaryPagination.hasMore)
        XCTAssertEqual(model.summaryPagination.items.first?.publicResponse, "Approved public response")
        XCTAssertNotNil(model.summaryPagination.items.first?.sentAt)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        CannedFeedURLProtocol.handlers[path + "/" + id + "/resolution"] = try (
            detail(fixture: "native.moderation.disputes.detail.default", key: "dispute", sent: true, resolved: true),
            200
        )
        try await model.resolve(XCTUnwrap(model.detailReviewDispute), action: .remove)
        XCTAssertEqual(model.summaryPagination.items.map(\.id), Array(ids.dropFirst()))
        XCTAssertEqual(model.summaryPagination.endCursor, "retained-cursor")
        XCTAssertTrue(model.summaryPagination.hasMore)

    }

    func testAppealDeliveryPreservesLoadedPagesAndCursor() async throws {
        let path = "/api/v1/appeals"
        try registerPages(path: path, fixture: "native.moderation.appeals", key: "appeals")
        let model = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        await model.load()
        await model.loadMore()
        let ids = model.summaryPagination.items.map(\.id)
        XCTAssertEqual(ids.count, 2)
        let id = try XCTUnwrap(ids.first)
        CannedFeedURLProtocol.handlers[path + "/" + id] = try (
            detail(fixture: "native.moderation.appeals.detail.default", key: "appeal", sent: false), 200
        )
        await model.loadDetail(id: id)
        let initial = try XCTUnwrap(model.detailModerationAppeal)
        XCTAssertNil(model.summaryPagination.items.first?.publicResponse)
        XCTAssertTrue(try XCTUnwrap(model.summaryPagination.items.first).hasDraft)
        CannedFeedURLProtocol.handlers[path + "/" + id + "/delivery"] = try (
            detail(fixture: "native.moderation.appeals.detail.default", key: "appeal", sent: true), 200
        )
        await model.send(initial)
        XCTAssertEqual(model.summaryPagination.items.map(\.id), ids)
        XCTAssertEqual(model.summaryPagination.endCursor, "retained-cursor")
        XCTAssertTrue(model.summaryPagination.hasMore)
        XCTAssertEqual(model.summaryPagination.items.first?.publicResponse, "Approved public response")
        XCTAssertNotNil(model.summaryPagination.items.first?.sentAt)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        CannedFeedURLProtocol.handlers[path + "/" + id + "/resolution"] = try (
            detail(fixture: "native.moderation.appeals.detail.default", key: "appeal", sent: true, resolved: true), 200
        )
        try await model.resolve(XCTUnwrap(model.detailModerationAppeal), action: .reduce)
        XCTAssertEqual(model.summaryPagination.items.map(\.id), Array(ids.dropFirst()))
        XCTAssertEqual(model.summaryPagination.endCursor, "retained-cursor")
        XCTAssertTrue(model.summaryPagination.hasMore)

    }

    private func registerPages(path: String, fixture: String, key: String) throws {
        var first = try XCTUnwrap(JSONSerialization.jsonObject(with:
            ApiFixtureLoader.data(fixture + ".default")) as? [String: Any])
        first["page_info"] = [
            "has_next_page": true, "start_cursor": "first-page", "end_cursor": "next-page"
        ]
        var second: [String: Any]
        if key == "disputes" {
            second = first
            var row = try XCTUnwrap((first[key] as? [[String: Any]])?.first)
            row["id"] = "second-dispute"
            second[key] = [row]
        } else {
            second = try XCTUnwrap(JSONSerialization.jsonObject(with:
                ApiFixtureLoader.data(fixture + ".page-2")) as? [String: Any])
        }
        second["page_info"] = [
            "has_next_page": true, "start_cursor": "second-page", "end_cursor": "retained-cursor"
        ]
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (JSONSerialization.data(withJSONObject: first), 200, 0),
            (JSONSerialization.data(withJSONObject: second), 200, 0)
        ]
    }

    private func detail(fixture: String, key: String, sent: Bool, resolved: Bool = false) throws -> Data {
        var envelope = try XCTUnwrap(JSONSerialization
            .jsonObject(with: ApiFixtureLoader.data(fixture)) as? [String: Any])
        var object = try XCTUnwrap(envelope[key] as? [String: Any])
        object["public_response"] = "Approved public response"
        object["approved_at"] = "2026-07-01T11:00:00Z"
        object["sent_at"] = sent ? "2026-07-01T12:00:00Z" : NSNull()
        if resolved {
            object["status"] = "resolved"
            object["resolved_at"] = "2026-07-01T13:00:00Z"
            object["resolution_action"] = key == "dispute" ? "remove" : "reduce"
        }
        envelope[key] = object
        return try JSONSerialization.data(withJSONObject: envelope)
    }
}

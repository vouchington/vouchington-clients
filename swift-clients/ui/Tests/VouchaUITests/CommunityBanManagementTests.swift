import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class CommunityBanManagementTests: NativeRouteSurfaceViewModelTestCase {
    private let banId = "00000000-0000-7000-8000-000000000901"
    private let userId = "00000000-0000-7000-8000-000000000904"
    private let base = "/api/v1/communities/builders/bans"

    func testIndividualLiftKeepsSiblingAndFetchesReasonOnDemand() async throws {
        let data = ApiFixtureLoader.data("native.communities.bans.active.default")
        CannedFeedURLProtocol.handlers[base + "/users/" + userId] = (data, 200)
        CannedFeedURLProtocol.handlers[base + "/" + banId] = (
            ApiFixtureLoader.data("native.communities.bans.detail.default"), 200
        )
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        await model.loadActiveBans(userId: userId)
        XCTAssertNil(model.activeBans.first?.reason)
        await model.loadBanDetail(banId: banId)
        XCTAssertEqual(model.banDetail?.reason, "Repeated spam")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var bans = try XCTUnwrap(object["community_bans"] as? [String: [String: Any]])
        let siblingId = "sibling-ban"
        var sibling = try XCTUnwrap(bans[banId])
        sibling["id"] = siblingId
        bans[siblingId] = sibling
        object["community_bans"] = bans
        object["results"] = [["id": banId], ["id": siblingId]]
        CannedFeedURLProtocol.handlers[base + "/users/" + userId] = try (
            JSONSerialization.data(withJSONObject: object),
            200
        )
        await model.loadActiveBans(userId: userId)
        CannedFeedURLProtocol.handlers[base + "/" + banId] = (
            ApiFixtureLoader.data("native.communities.bans.lift-one.default"), 200
        )
        await model.liftBan(banId: banId)
        XCTAssertEqual(model.activeBans.map(\.id), [siblingId])
        XCTAssertNil(model.banDetail)
    }

    func testLiftPreservesHistoryContinuationAndRejectsStalePage() async throws {
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .bans)
        let row = NativeRouteDestinationRow(icon: "nosign", title: .verbatim("ban"), detail: .verbatim(""))
        let request = try XCTUnwrap(model.rowPagination.beginNextPage())
        XCTAssertTrue(model.rowPagination.complete(
            request, items: [CommunityForwardRow(id: banId, row: row), CommunityForwardRow(id: "sibling", row: row)],
            endCursor: "opaque-next", hasNextPage: true
        ))
        model.summary.rows = model.rowPagination.items.map(\.row)
        let stale = try XCTUnwrap(model.rowPagination.beginNextPage())
        CannedFeedURLProtocol.handlers[base + "/" + banId] = (
            ApiFixtureLoader.data("native.communities.bans.lift-one.default"), 200
        )
        await model.liftBan(banId: banId)
        XCTAssertEqual(model.rowPagination.items.map(\.id), ["sibling"])
        XCTAssertEqual(model.summary.rows.count, 1)
        XCTAssertEqual(model.rowPagination.endCursor, "opaque-next")
        XCTAssertTrue(model.rowPagination.hasMore)
        XCTAssertFalse(model.rowPagination.complete(
            stale, items: [CommunityForwardRow(id: banId, row: row)], endCursor: nil, hasNextPage: false
        ))
        XCTAssertEqual(model.rowPagination.beginNextPage()?.cursor, "opaque-next")
    }

    func testLiftAllRequiresConfirmationAndUsesReturnedIds() async throws {
        CannedFeedURLProtocol.handlers[base + "/users/" + userId] = (
            ApiFixtureLoader.data("native.communities.bans.active.default"), 200
        )
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        await model.loadActiveBans(userId: userId)
        let count = CannedFeedURLProtocol.capturedRequests.count
        await model.confirmLiftAllBans()
        model.requestLiftAllBans(userId: userId)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.count, count)
        CannedFeedURLProtocol.handlers[base + "/users/" + userId] = (
            ApiFixtureLoader.data("native.communities.bans.lift-all.default"), 200
        )
        await model.confirmLiftAllBans()
        XCTAssertTrue(model.activeBans.isEmpty)
        XCTAssertNil(model.pendingLiftAllUserId)
    }

    func testLocalizedForbiddenAndOverflow() async throws {
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        for (fixture, status, key) in [
            ("native.communities.bans.lift-all.forbidden", 403, UiMessageKey.moderationCommunityBansLiftAllForbidden),
            ("native.communities.bans.lift-all.overflow", 409, UiMessageKey.moderationCommunityBansLiftAllLimit)
        ] {
            CannedFeedURLProtocol.handlers[base + "/users/" + userId] = (ApiFixtureLoader.data(fixture), status)
            model.requestLiftAllBans(userId: userId)
            await model.confirmLiftAllBans()
            XCTAssertEqual(model.banError?.key, key)
            if status == 409 {
                XCTAssertEqual(model.banError?.numberParameters["cap"], 1)
            }
        }
    }

    func testHistoryAndActiveAppendOpaqueCursorPages() async throws {
        let data = ApiFixtureLoader.data("native.communities.bans.active.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        object["page_info"] = ["has_next_page": true, "end_cursor": "opaque+/cursor="]
        let page = try JSONSerialization.data(withJSONObject: object)
        CannedFeedURLProtocol.handlers[base] = (page, 200)
        CannedFeedURLProtocol.handlers[base + "/users/" + userId] = (page, 200)
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        await model.loadBanHistory()
        await model.loadBanHistory(more: true)
        await model.loadActiveBans(userId: userId)
        await model.loadActiveBans(userId: userId, more: true)
        XCTAssertEqual(model.banHistory.map(\.id), [banId])
        XCTAssertEqual(model.activeBans.map(\.id), [banId])
        let continued = CannedFeedURLProtocol.capturedRequests.filter { URLComponents(
            url: $0.url,
            resolvingAgainstBaseURL: false
        )?.queryItems?.first { $0.name == "after" }?.value == "opaque+/cursor=" }
        XCTAssertEqual(continued.count, 2)
    }
}

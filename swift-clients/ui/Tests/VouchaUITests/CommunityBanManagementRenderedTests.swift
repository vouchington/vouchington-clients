import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class CommunityBanManagementRenderedTests: NativeRouteSurfaceViewModelTestCase {
    private let banId = "00000000-0000-7000-8000-000000000901"
    private let userId = "00000000-0000-7000-8000-000000000904"
    private let base = "/api/v1/communities/builders/bans"
    private let locale = Locale.current

    private func pagedFixture() throws -> Data {
        let data = ApiFixtureLoader.data("native.communities.bans.active.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        object["page_info"] = ["has_next_page": true, "end_cursor": "opaque-next"]
        return try JSONSerialization.data(withJSONObject: object)
    }

    private func title(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: locale)
    }

    private func buttons(
        _ view: CommunityBanManagementControls, titled title: String
    ) throws -> [InspectableView<ViewType.Button>] {
        try view.inspect().findAll(ViewType.Button.self).filter {
            (try? $0.labelView().text().string(locale: locale)) == title
        }
    }

    private func tap(_ view: CommunityBanManagementControls, _ title: String, index: Int = 0) throws {
        try buttons(view, titled: title)[index].tap()
    }

    private func wait(_ description: String, _ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 200 where !condition() {
            try? await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTAssertTrue(condition(), description)
    }

    private func afterCursorRequests(path: String) -> Int {
        CannedFeedURLProtocol.capturedRequests.filter {
            $0.url.path == path && URLComponents(url: $0.url, resolvingAgainstBaseURL: false)?
                .queryItems?.contains { $0.name == "after" && $0.value == "opaque-next" } == true
        }.count
    }

    func testHistoryRowsLoadMoreDetailAndLiftRenderAndSendRequests() async throws {
        CannedFeedURLProtocol.handlers[base] = try (pagedFixture(), 200)
        CannedFeedURLProtocol.handlers[base + "/" + banId] = (
            ApiFixtureLoader.data("native.communities.bans.detail.default"), 200
        )
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        let view = CommunityBanManagementControls(viewModel: model)
        try await ViewHosting.host(view) {
            try tap(view, title(.nativeSwiftCommunitiesBans))
            await wait("history loads") { !model.banHistory.isEmpty }
            XCTAssertNoThrow(try view.inspect().find(text: userId))
            try tap(view, title(.nativeSwiftCommonLoadMore))
            await wait("next page requested") { self.afterCursorRequests(path: self.base) == 1 }
            try tap(view, banId)
            await wait("detail loads") { model.banDetail != nil }
            XCTAssertNoThrow(try view.inspect().find(text: "Repeated spam"))
            CannedFeedURLProtocol.handlers[base + "/" + banId] = (
                ApiFixtureLoader.data("native.communities.bans.lift-one.default"), 200
            )
            try tap(view, title(.nativeSwiftCommunityActionsLiftBan))
            await wait("ban lifted") { model.banHistory.isEmpty && model.banDetail == nil }
            XCTAssertThrowsError(try view.inspect().find(text: "Repeated spam"))
            XCTAssertThrowsError(try view.inspect().find(text: userId))
        }
    }

    func testActiveBansLoadMoreAndLiftAllConfirmation() async throws {
        CannedFeedURLProtocol.handlers[base + "/users/" + userId] = try (pagedFixture(), 200)
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        let view = CommunityBanManagementControls(viewModel: model)
        try await ViewHosting.host(view) {
            XCTAssertTrue(try buttons(view, titled: title(.moderationCommunityBansLiftAll)).isEmpty)
            XCTAssertNoThrow(try view.inspect().find(ViewType.TextField.self))
            await model.loadActiveBans(userId: userId)
            await wait("active bans load") { !model.activeBans.isEmpty }
            XCTAssertNoThrow(try view.inspect().find(text: userId))
            try tap(view, title(.nativeSwiftCommonLoadMore))
            await wait("next active page requested") {
                self.afterCursorRequests(path: self.base + "/users/" + self.userId) == 1
            }
            CannedFeedURLProtocol.handlers[base + "/users/" + userId] = (
                ApiFixtureLoader.data("native.communities.bans.lift-all.default"), 200
            )
            try tap(view, title(.moderationCommunityBansLiftAll))
            XCTAssertEqual(model.pendingLiftAllUserId, userId)
            await model.confirmLiftAllBans()
            await wait("all bans lifted") { model.activeBans.isEmpty && model.pendingLiftAllUserId == nil }
            XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.contains {
                $0.url.path == self.base + "/users/" + self.userId && $0.method != "GET"
            })
        }
    }

    func testFailuresRenderLocalizedErrors() async throws {
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        let view = CommunityBanManagementControls(viewModel: model)
        CannedFeedURLProtocol.handlers[base] = (Data(#"{"error":"down"}"#.utf8), 500)
        CannedFeedURLProtocol.handlers[base + "/" + banId] = (Data(#"{"error":"down"}"#.utf8), 500)
        try await ViewHosting.host(view) {
            try tap(view, title(.nativeSwiftCommunitiesBans))
            await wait("history error") { model.banError != nil }
            XCTAssertNoThrow(try view.inspect().find(
                text: UiMessages.string(.nativeSwiftEmptyStateUnableToLoad, locale: locale)
            ))
        }
        await model.loadBanDetail(banId: banId)
        XCTAssertEqual(model.banError?.key, .moderationCommunityBansDetailLoadFailed)
        CannedFeedURLProtocol.handlers[base + "/users/" + userId] = (
            Data(#"{"code":"COMMUNITY_BAN_LIFT_ALL_LIMIT_REACHED","message":"limit"}"#.utf8),
            409
        )
        model.requestLiftAllBans(userId: userId)
        await model.confirmLiftAllBans()
        XCTAssertEqual(model.banError?.key, .moderationCommunityBansLiftAllLimitUnknown)
        try await ViewHosting.host(view) {
            XCTAssertNoThrow(try view.inspect().find(
                text: UiMessages.string(.moderationCommunityBansLiftAllLimitUnknown, locale: locale)
            ))
        }
    }
}

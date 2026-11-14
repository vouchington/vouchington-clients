import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativePublicLandingItemsTests: NativeRouteSurfaceViewModelTestCase {
    func testRenderedReviewAndGroupedMemberActivateNativeRouteAndExactClickBody() async throws {
        let model = try makeModel()
        let page = try await loadPage(model)
        var navigations: [String] = []
        let sut = NativePublicLandingItemsSurface(page: page, viewModel: model, onNavigate: { navigations.append($0) })
        let review = try sut.inspect().find(viewWithAccessibilityIdentifier: "landing-item-item-review-1").button()
        try review.tap()
        await settleProductSignals(model)
        try review.tap()
        await settleProductSignals(model)
        let group = try sut.inspect().find(viewWithAccessibilityIdentifier: "landing-item-group-entry-review-1")
            .button()
        try group.tap()
        await settleProductSignals(model)
        let clicks = try clickBodies()
        XCTAssertEqual(clicks.count, 3)
        XCTAssertEqual(clicks[0], ["landing_page_item_id": "item-review-1"])
        XCTAssertEqual(clicks[1], clicks[0])
        XCTAssertEqual(
            clicks[2],
            ["landing_page_item_id": "item-topic-group-1", "group_member_id": "group-entry-review-1"]
        )
        XCTAssertEqual(navigations.count, 3)
        XCTAssertTrue(navigations.allSatisfy { $0.hasPrefix("/review/") })
    }

    func testRenderedExternalTargetsTrackParentAndGroupMember() async throws {
        let model = try makeModel()
        let page = try await loadPage(model)
        var opened: [URL] = []
        let sut = NativePublicLandingItemsSurface(
            page: page, viewModel: model, onNavigate: { _ in XCTFail("Unexpected navigation") },
            onOpenExternal: { opened.append($0) }
        )
        for id in ["item-profile-1", "item-referral-1", "item-link-1", "group-entry-referral-1"] {
            try sut.inspect().find(viewWithAccessibilityIdentifier: "landing-item-\(id)").button().tap()
            await settleProductSignals(model)
        }
        let clicks = try clickBodies()
        XCTAssertEqual(opened.count, 4)
        XCTAssertEqual(clicks.map { $0["landing_page_item_id"] }, [
            "item-profile-1", "item-referral-1", "item-link-1", "item-topic-group-1"
        ])
        XCTAssertEqual(clicks.last?["group_member_id"], "group-entry-referral-1")
        XCTAssertThrowsError(try sut.inspect().find(viewWithAccessibilityIdentifier: "landing-item-item-topic-group-1"))
    }

    func testSignedOutRenderedActivationNavigatesWithoutClick() async throws {
        let model = try makeModel(signedIn: false)
        let page = try await loadPage(model)
        var navigated = false
        let sut = NativePublicLandingItemsSurface(page: page, viewModel: model, onNavigate: { _ in navigated = true })
        try sut.inspect().find(viewWithAccessibilityIdentifier: "landing-item-item-review-1").button().tap()
        await settleProductSignals(model)
        XCTAssertTrue(navigated)
        XCTAssertTrue(try clickBodies().isEmpty)
    }

    func testGroupHeadingAndInvalidExternalLinkHaveNoActivation() async throws {
        let model = try makeModel()
        var object = try XCTUnwrap(try JSONSerialization
            .jsonObject(with: publicPageData())
            as? [String: Any])
        var pageObject = try XCTUnwrap(object["landing_page"] as? [String: Any])
        pageObject["items"] = [["id": "unsafe", "type": "link", "label": "Invalid", "url": "javascript:alert(1)"]]
        object["landing_page"] = pageObject
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = try (
            JSONSerialization.data(withJSONObject: object), 200
        )
        await model.load()
        let page = try XCTUnwrap(model.publicLandingPage)
        let sut = NativePublicLandingItemsSurface(
            page: page,
            viewModel: model,
            onNavigate: { _ in XCTFail("Unexpected navigation") }
        )
        XCTAssertThrowsError(try sut.inspect().find(viewWithAccessibilityIdentifier: "landing-item-unsafe"))
        await settleProductSignals(model)
        XCTAssertTrue(try clickBodies().isEmpty)
    }

    func testSignalCapacityDropsExcessWithoutWaitingAndInvalidationFencesOldModel() async throws {
        let model = try makeModel()
        let path = "/api/v1/landing-pages/page-1/clicks"
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        for _ in 0 ..< 10 {
            model.recordPublicLandingItemClick(landingPageId: "page-1", itemId: "item-1")
        }
        XCTAssertEqual(model.productSignalTasks.count, 4)
        _ = try await barrier.wait()
        model.invalidateProductSignals()
        model.recordPublicLandingItemClick(landingPageId: "page-1", itemId: "item-1")
        CannedFeedURLProtocol.releaseResponse(path: path)
        await settleProductSignals(model)
        XCTAssertTrue(model.productSignalTasks.isEmpty)
        XCTAssertLessThanOrEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 4)
    }

    func testNewActivationRecordsVisitAgainButReloadDoesNot() async throws {
        let model = try makeModel()
        model.beginProductSurfaceActivation()
        _ = try await loadPage(model)
        model.beginProductSurfaceActivation()
        _ = try await loadPage(model)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path.hasSuffix("/visits") }.count, 1)
        model.endProductSurfaceActivation()
        model.beginProductSurfaceActivation()
        _ = try await loadPage(model)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path.hasSuffix("/visits") }.count, 2)
    }

    func testDisappearanceKeepsAlreadyDispatchedClick() async throws {
        let model = try makeModel()
        let path = "/api/v1/landing-pages/page-1/clicks"
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        model.recordPublicLandingItemClick(landingPageId: "page-1", itemId: "item-1")
        _ = try await barrier.wait()
        model.endProductSurfaceActivation()
        XCTAssertEqual(model.productSignalTasks.count, 1)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await settleProductSignals(model)
        XCTAssertEqual(try clickBodies(), [["landing_page_item_id": "item-1"]])
        XCTAssertTrue(model.productSignalTasks.isEmpty)
    }

    func testPublicEnvelopeRequiresUserButAllowsMissingOrNullUsername() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: publicPageData()) as? [String: Any])
        XCTAssertNoThrow(try decoder.decode(NativePublicLandingPageResponse.self, from: publicPageData()))
        object.removeValue(forKey: "user")
        XCTAssertThrowsError(try decoder.decode(
            NativePublicLandingPageResponse.self, from: JSONSerialization.data(withJSONObject: object)
        ))
        for owner in [[:], ["username": NSNull()]] as [[String: Any]] {
            object["user"] = owner
            let response = try decoder.decode(
                NativePublicLandingPageResponse.self, from: JSONSerialization.data(withJSONObject: object)
            )
            XCTAssertNil(response.user.username)
        }
    }

}

private extension NativePublicLandingItemsTests {
    func loadPage(_ model: NativeRouteSurfaceViewModel) async throws -> NativePublicLandingPage {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = try (
            publicPageData(), 200
        )
        await model.load()
        await settleProductSignals(model)
        return try XCTUnwrap(model.publicLandingPage)
    }

    func publicPageData() throws -> Data {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with:
            ApiFixtureLoader.data("native.landing-page-detail.default")) as? [String: Any])
        object["user"] = ["username": "alice"]
        return try JSONSerialization.data(withJSONObject: object)
    }

    func makeModel(signedIn: Bool = true) throws -> NativeRouteSurfaceViewModel {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/bonus"))
        return try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match, isSignedIn: signedIn
        )
    }

    func clickBodies() throws -> [[String: String]] {
        try CannedFeedURLProtocol.capturedRequests.filter { $0.url.path.hasSuffix("/clicks") }.map {
            try XCTUnwrap(JSONSerialization.jsonObject(with: Data(($0.body ?? "").utf8)) as? [String: String])
        }
    }
}

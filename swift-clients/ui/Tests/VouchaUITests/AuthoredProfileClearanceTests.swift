import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class AuthoredProfileClearanceTests: NativeRouteSurfaceViewModelTestCase {
    func testRejectedProfileRowShowsOwnerAppealAndHidesBodyFromOtherViewer() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = try (rejectedProfileResponse(), 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/posts"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match
        )
        await viewModel.load()
        var navigated: [String] = []
        let author = NativeUserProfileSurface(
            entry: route.entry, viewModel: viewModel, isSignedIn: true,
            currentUserId: "user-1", turnstileSiteKey: nil,
            showSignIn: {}, onNavigate: { navigated.append($0) }
        )
        try author.environment(\.locale, Locale(identifier: "en_US")).inspect().find(button: "View appeals").tap()
        XCTAssertEqual(navigated, ["/my/appeals"])
        XCTAssertNoThrow(try author.environment(\.locale, Locale(identifier: "en_US"))
            .inspect().find(text: "This content was removed by a moderator."))
        XCTAssertThrowsError(try author.inspect().find(text: "Authored profile review"))

        let other = NativeUserProfileSurface(
            entry: route.entry, viewModel: viewModel, isSignedIn: true,
            currentUserId: "other", turnstileSiteKey: nil,
            showSignIn: {}, onNavigate: { _ in }
        )
        XCTAssertNoThrow(try other.environment(\.locale, Locale(identifier: "en_US"))
            .inspect().find(text: "This content is unavailable."))
        XCTAssertThrowsError(try other.inspect().find(button: "View appeals"))
        XCTAssertThrowsError(try other.inspect().find(text: "Authored profile review"))
    }

    private func rejectedProfileResponse() throws -> Data {
        var body = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data("native.users.profile.posts.all"))
                as? [String: Any]
        )
        var posts = try XCTUnwrap(body["posts"] as? [String: [String: Any]])
        var review = try XCTUnwrap(posts["profile-review-1"])
        review["clearance_status"] = "rejected"
        posts["profile-review-1"] = review
        body["posts"] = posts
        return try JSONSerialization.data(withJSONObject: body)
    }
}

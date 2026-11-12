import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class UserProfileCappedCountTests: NativeRouteSurfaceViewModelTestCase {
    func testProfileHeaderShowsLowerBoundForEachCappedMetricAndTheirSum() throws {
        let profile = try makeProfile(capped: true)
        let surface = try makeSurface()
        let counts = surface.profileCounts(profile)

        XCTAssertNoThrow(try counts.inspect().find(text: "1,005+ posts"))
        XCTAssertNoThrow(try counts.inspect().find(text: "1,000+ following"))
        XCTAssertNoThrow(try counts.inspect().find(text: "1,000+ followers"))
    }

    func testExactCountsAtTheCapHaveNoPlus() throws {
        let counts = try makeSurface().profileCounts(makeProfile(capped: false))
        XCTAssertNoThrow(try counts.inspect().find(text: "1,005 posts"))
        XCTAssertNoThrow(try counts.inspect().find(text: "1,000 following"))
        XCTAssertNoThrow(try counts.inspect().find(text: "1,000 followers"))
    }

    func testLowerBoundNumberUsesTheMessageLocaleAndKeepsPluralSelection() {
        let message = UiMessage(
            .nativeSwiftProfileFollowersCount,
            numberParameters: ["count": 1_000],
            cappedNumberParameters: ["count"]
        )
        XCTAssertEqual(UiMessages.string(message, locale: Locale(identifier: "en")), "1,000+ followers")
        XCTAssertEqual(UiMessages.string(message, locale: Locale(identifier: "fr")), "1\u{202F}000+ abonnés")
        XCTAssertEqual(UiMessages.string(
            UiMessage(.nativeSwiftProfileFollowersCount, numberParameters: ["count": 1]),
            locale: Locale(identifier: "en")
        ), "1 follower")
    }

    func testPrivateCountsShowSparseKeysWithTheirOwnCapFlags() throws {
        let profile = try makeProfile(capped: false, privateCounts: true)
        let surface = try makeSurface(currentUserId: "user-abc")
        let rows = surface.privateCollectionCounts(profile)
        XCTAssertEqual(rows.map(\.id), ["posts_saved", "communities_saved", "urls_saved"])
        XCTAssertEqual(rows.map(\.count), [1_000, 2, 1_000])
        XCTAssertEqual(rows.map(\.capped), [true, false, false])
        let view = surface.privateCollectionCountsView(profile)
        XCTAssertNoThrow(try view.inspect().find(text: "Saved posts"))
        XCTAssertNoThrow(try view.inspect().find(text: "1,000+"))
        XCTAssertNoThrow(try view.inspect().find(text: "1,000"))
        XCTAssertNoThrow(try view.inspect().find(text: "2"))
    }

    func testPrivateCountsStayHiddenForOtherUsersSignedOutAndNonOverviewScopes() throws {
        let profile = try makeProfile(capped: false, privateCounts: true)
        XCTAssertTrue(try makeSurface(currentUserId: "another-user").privateCollectionCounts(profile).isEmpty)
        XCTAssertTrue(try makeSurface(currentUserId: "user-abc", signedIn: false)
            .privateCollectionCounts(profile).isEmpty)
        let surface = try makeSurface(currentUserId: "user-abc")
        surface.viewModel.userProfile.scope = .posts(.all)
        XCTAssertTrue(surface.privateCollectionCounts(profile).isEmpty)
        XCTAssertTrue(try makeSurface(currentUserId: "user-abc")
            .privateCollectionCounts(makeProfile(capped: false)).isEmpty)
    }

    private func makeProfile(capped: Bool, privateCounts: Bool = false) throws -> UserProfileResponse {
        var root = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.users.profile.default")
        ) as? [String: Any])
        root["user_metrics"] = [
            "id": "user-abc",
            "count": [
                "reviews": 1_000,
                "discussions": 3,
                "comments": 2,
                "users_following": 1_000,
                "users_followers": 1_000
            ],
            "count_capped": [
                "reviews": capped,
                "discussions": false,
                "comments": false,
                "users_following": capped,
                "users_followers": capped
            ]
        ]
        if privateCounts {
            var metrics = try XCTUnwrap(root["user_metrics"] as? [String: Any])
            metrics["private_count"] = ["posts_saved": 1_000, "communities_saved": 2, "urls_saved": 1_000]
            metrics["private_count_capped"] = ["posts_saved": true, "communities_saved": false]
            root["user_metrics"] = metrics
        }
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(UserProfileResponse.self, from: JSONSerialization.data(withJSONObject: root))
    }

    private func makeSurface(currentUserId: String? = nil, signedIn: Bool = true) throws -> NativeUserProfileSurface {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match
        )
        viewModel.userProfile.scope = .overview
        return NativeUserProfileSurface(
            entry: route.entry, viewModel: viewModel, isSignedIn: signedIn,
            currentUserId: currentUserId,
            turnstileSiteKey: nil, showSignIn: {}, onNavigate: { _ in }
        )
    }
}

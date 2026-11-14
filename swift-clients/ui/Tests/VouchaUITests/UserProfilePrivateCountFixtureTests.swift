import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class UserProfilePrivateCountFixtureTests: NativeRouteSurfaceViewModelTestCase {
    private static let ownerFixture = "native.users.profile.private-counts.owner"
    private static let cappedMetrics: Set<String> = [
        "rss_feed_items_hidden", "rss_feed_items_saved", "topics_viewed"
    ]
    private static let expectedLabels: [String: UiMessageKey] = [
        "rss_feed_items_saved": .nativeDotnetBookmarksSavedRssItems,
        "rss_feed_items_hidden": .extractedUsersUserRssFeedItemRoutesHiddenRssItems28d66515,
        "rss_feed_items_viewed": .nativeDotnetBookmarksViewedRssItems,
        "rss_feeds_subscribed": .nativeDotnetBookmarksSubscribedRssFeeds,
        "rss_feeds_muted": .nativeDotnetBookmarksMutedRssFeeds,
        "rss_feeds_viewed": .nativeDotnetBookmarksViewedRssFeeds,
        "topics_subscribed_posts": .nativeDotnetBookmarksSubscribedTopicsForPosts,
        "topics_subscribed_news": .nativeDotnetBookmarksSubscribedTopicsForNews,
        "users_subscribed_posts": .nativeDotnetBookmarksSubscribedUsersForPosts,
        "posts_subscribed": .nativeDotnetBookmarksSubscribedToPosts
    ]

    func testOwnerFixtureRendersEveryPrivateMetricWithCappedValuesAsLowerBounds() throws {
        let profile = try decode(Self.ownerFixture)
        let metrics = try XCTUnwrap(profile.userMetrics?.privateCount)
        XCTAssertEqual(metrics.count, 26)
        let surface = try makeSurface(currentUserId: "user-abc")
        XCTAssertEqual(Set(surface.privateCollectionCounts(profile).map(\.id)), Set(metrics.keys))
        surface.viewModel.userProfile.header = profile
        let rendered = try surface.environment(\.locale, Locale(identifier: "en")).inspect()
        for (key, value) in metrics {
            let row = try rendered.find(ViewType.HStack.self, where: {
                try $0.accessibilityIdentifier() == "private-collection-count-\(key)"
            })
            let capped = Self.cappedMetrics.contains(key)
            XCTAssertEqual(value == 1_000, capped, key)
            XCTAssertNoThrow(try row.find(text: UiMessages.cappedNumber(
                Double(value), capped: capped, locale: Locale(identifier: "en")
            )), key)
        }
        let saved = try rendered.find(ViewType.HStack.self, where: {
            try $0.accessibilityIdentifier() == "private-collection-count-rss_feed_items_saved"
        })
        XCTAssertNoThrow(try saved.find(text: "Saved RSS Items"))
        XCTAssertNoThrow(try saved.find(text: "1,000+"))
    }

    func testEachNewPrivateMetricUsesItsOwnLabel() throws {
        let surface = try makeSurface(currentUserId: "user-abc")
        let rows = try surface.privateCollectionCounts(decode(Self.ownerFixture))
        for (key, label) in Self.expectedLabels {
            XCTAssertEqual(rows.first { $0.id == key }?.title, label, key)
        }
        XCTAssertEqual(Set(rows.map(\.title)).count, rows.count)
    }

    func testNonOwnerFixturesAndOtherViewersShowNoPrivateRows() throws {
        for id in ["native.users.profile.restricted", "native.users.profile.default"] {
            let profile = try decode(id)
            XCTAssertNil(profile.userMetrics?.privateCount, id)
            XCTAssertTrue(try makeSurface(currentUserId: "user-abc").privateCollectionCounts(profile).isEmpty, id)
        }
        let owner = try decode(Self.ownerFixture)
        XCTAssertTrue(try makeSurface(currentUserId: "someone-else").privateCollectionCounts(owner).isEmpty)
    }

    private func decode(_ id: String) throws -> UserProfileResponse {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(UserProfileResponse.self, from: ApiFixtureLoader.data(id))
    }

    private func makeSurface(currentUserId: String) throws -> NativeUserProfileSurface {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match
        )
        viewModel.userProfile.scope = .overview
        return NativeUserProfileSurface(
            entry: route.entry, viewModel: viewModel, isSignedIn: true, currentUserId: currentUserId,
            turnstileSiteKey: nil, showSignIn: {}, onNavigate: { _ in }
        )
    }
}

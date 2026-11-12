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
        XCTAssertEqual(rows.map(\.id), ["posts_saved", "communities_saved", "rss_feed_items_saved", "urls_saved"])
        XCTAssertEqual(rows.map(\.count), [1_000, 2, 5, 1_000])
        XCTAssertEqual(rows.map(\.capped), [true, false, true, false])
        let view = surface.privateCollectionCountsView(profile)
        XCTAssertNoThrow(try view.inspect().find(text: "Saved posts"))
        XCTAssertNoThrow(try view.inspect().find(text: "Saved feed items"))
        XCTAssertNoThrow(try view.inspect().find(text: "5+"))
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

    func testExactViewerCountsSuppressPublicCapsIncludingZeroAndPostAggregate() throws {
        let profile = try makeProfile(capped: true, exactViewerCounts: true)
        let counts = try makeSurface().profileCounts(profile)
        XCTAssertNoThrow(try counts.inspect().find(text: "9 posts"))
        XCTAssertNoThrow(try counts.inspect().find(text: "0 following"))
        XCTAssertNoThrow(try counts.inspect().find(text: "7 followers"))
        XCTAssertThrowsError(try counts.inspect().find(text: "9+ posts"))
        XCTAssertThrowsError(try counts.inspect().find(text: "0+ following"))
        XCTAssertThrowsError(try counts.inspect().find(text: "7+ followers"))
    }

    func testEveryPrivateCollectionRendersItsOwnLocalizedLabelAndCapFlag() throws {
        let expected: [(String, UiMessageKey)] = [
            ("posts_saved", .nativeSwiftRouteSurfaceSavedPosts),
            ("communities_saved", .nativeSwiftRouteSurfaceSavedCommunities),
            ("rss_feed_items_saved", .nativeSwiftRouteSurfaceSavedFeedItems),
            ("urls_saved", .nativeSwiftRouteSurfaceSavedUrls),
            ("posts_hidden", .nativeDotnetBookmarksHiddenPosts),
            ("posts_following", .nativeDotnetBookmarksFollowedPosts),
            ("topics_blocked", .nativeDotnetBookmarksBlockedTopics),
            ("topics_muted", .nativeDotnetBookmarksMutedTopics),
            ("topics_viewed", .nativeDotnetBookmarksViewedTopics),
            ("topics_dismissed_recommendations", .nativeDotnetBookmarksDismissedTopicRecommendations),
            ("users_blocked", .nativeDotnetBookmarksBlockedUsers),
            ("users_muted", .nativeDotnetBookmarksMutedUsers),
            ("users_subscribed_posts", .nativeDotnetBookmarksSubscribedToPosts),
            ("users_dismissed_recommendations", .nativeDotnetBookmarksDismissedFriendRecommendations),
            ("rss_feed_items_hidden", .extractedUsersUserRssFeedItemRoutesHiddenRssItems28d66515),
            ("domains_blocked", .nativeDotnetBookmarksBlockedDomains),
            ("domains_muted", .nativeDotnetBookmarksMutedDomains),
            ("communities_proxy_following", .nativeDotnetBookmarksProxyFollowedCommunities),
            ("communities_proxy_muted", .nativeDotnetBookmarksProxyMutedCommunities)
        ]
        let counts = Dictionary(uniqueKeysWithValues: expected.enumerated().map { ($1.0, 1_000 + $0) })
        let caps = Dictionary(uniqueKeysWithValues: expected.enumerated().map { ($1.0, $0.isMultiple(of: 2)) })
        let profile = try makeProfile(capped: false, suppliedPrivateCounts: counts, suppliedCaps: caps)
        for locale in [Locale(identifier: "en"), Locale(identifier: "fr")] {
            let surface = try makeSurface(currentUserId: "user-abc")
            surface.viewModel.userProfile.header = profile
            let rendered = try surface.environment(\.locale, locale).inspect()
            for (index, entry) in expected.enumerated() {
                let row = try rendered.find(ViewType.HStack.self, where: {
                    try $0.accessibilityIdentifier() == "private-collection-count-\(entry.0)"
                })
                XCTAssertNoThrow(try row.find(text: UiMessages.string(entry.1, locale: locale)), entry.0)
                XCTAssertNoThrow(try row.find(text: UiMessages.cappedNumber(
                    Double(1_000 + index), capped: index.isMultiple(of: 2), locale: locale
                )), entry.0)
            }
        }
    }

    func testSparsePrivateMetricsRenderZeroIgnoreUnknownAndDefaultMissingCapToExact() throws {
        let profile = try makeProfile(
            capped: false,
            suppliedPrivateCounts: ["posts_hidden": 0, "topics_muted": 1_000, "future_metric": 5],
            suppliedCaps: ["future_metric": true, "rss_feed_items_hidden": true]
        )
        let surface = try makeSurface(currentUserId: "user-abc")
        let rows = surface.privateCollectionCounts(profile)
        XCTAssertEqual(rows.map(\.id), ["posts_hidden", "topics_muted"])
        let rendered = try surface.privateCollectionCountsView(profile).inspect()
        let hidden = try rendered.find(ViewType.HStack.self, where: {
            try $0.accessibilityIdentifier() == "private-collection-count-posts_hidden"
        })
        XCTAssertNoThrow(try hidden.find(text: "0"))
        let muted = try rendered.find(ViewType.HStack.self, where: {
            try $0.accessibilityIdentifier() == "private-collection-count-topics_muted"
        })
        XCTAssertNoThrow(try muted.find(text: "1,000"))
        XCTAssertThrowsError(try rendered.find(text: "5+"))
        XCTAssertThrowsError(try rendered.find(text: "1,000+"))
    }

    func testPrivateMetricRowsDoNotRenderOutsideOwnerOverview() throws {
        let profile = try makeProfile(
            capped: false,
            suppliedPrivateCounts: ["posts_hidden": 1, "users_muted": 2, "rss_feed_items_hidden": 3]
        )
        for (viewer, signedIn, overview) in [
            ("another-user", true, true), ("user-abc", false, true), ("user-abc", true, false)
        ] {
            let surface = try makeSurface(currentUserId: viewer, signedIn: signedIn)
            if !overview { surface.viewModel.userProfile.scope = .posts(.all) }
            let rendered = try surface.privateCollectionCountsView(profile).inspect()
            XCTAssertThrowsError(try rendered.find(ViewType.HStack.self, where: {
                try $0.accessibilityIdentifier() == "private-collection-count-posts_hidden"
            }))
            XCTAssertTrue(surface.privateCollectionCounts(profile).isEmpty)
        }
    }

    private func makeProfile(
        capped: Bool,
        privateCounts: Bool = false,
        exactViewerCounts: Bool = false,
        suppliedPrivateCounts: [String: Int]? = nil,
        suppliedCaps: [String: Bool]? = nil
    ) throws -> UserProfileResponse {
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
        if exactViewerCounts {
            var metrics = try XCTUnwrap(root["user_metrics"] as? [String: Any])
            metrics["viewer_count"] = [
                "reviews": 4,
                "comments": 2,
                "discussions": 3,
                "users_following": 0,
                "users_followers": 7
            ]
            root["user_metrics"] = metrics
        }
        if privateCounts {
            var metrics = try XCTUnwrap(root["user_metrics"] as? [String: Any])
            metrics["private_count"] = [
                "posts_saved": 1_000,
                "communities_saved": 2,
                "rss_feed_items_saved": 5,
                "urls_saved": 1_000
            ]
            metrics["private_count_capped"] = [
                "posts_saved": true,
                "communities_saved": false,
                "rss_feed_items_saved": true
            ]
            root["user_metrics"] = metrics
        }
        if let suppliedPrivateCounts {
            var metrics = try XCTUnwrap(root["user_metrics"] as? [String: Any])
            metrics["private_count"] = suppliedPrivateCounts
            metrics["private_count_capped"] = suppliedCaps ?? [:]
            root["user_metrics"] = metrics
        }
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
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

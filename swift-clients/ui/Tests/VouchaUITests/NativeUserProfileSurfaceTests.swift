import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeUserProfileSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testHeaderPrefersDisplayAccountNameOverVerifiedNameAndUsername() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = try (
            profileHeaderFixture(
                displayAccountName: "Alice Display",
                verifiedDisplayName: "Alice Verified"
            ),
            200
        )
        let surface = try await surface(for: "/user/alice")

        XCTAssertNoThrow(try surface.inspect().find(text: "Alice Display"))
        XCTAssertThrowsError(try surface.inspect().find(text: "Alice Verified"))

        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = try (
            profileHeaderFixture(verifiedDisplayName: "Alice Verified"),
            200
        )
        let verifiedSurface = try await self.surface(for: "/user/alice")
        XCTAssertNoThrow(try verifiedSurface.inspect().find(text: "Alice Verified"))

        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = try (profileHeaderFixture(), 200)
        let usernameSurface = try await self.surface(for: "/user/alice")
        XCTAssertNoThrow(try usernameSurface.inspect().find(text: "alice"))
    }

    func testBuiltInSocialProfileLinkResolvesHandleBeforeURLFallback() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = try (
            profileHeaderFixture(
                profileLinkType: "github",
                profileLinkHandle: "octocat",
                profileLinkURL: "https://fallback.example/profile"
            ),
            200
        )
        let surface = try await surface(for: "/user/alice")
        let link = try XCTUnwrap(surface.viewModel.userProfile.header?.profileLinks.first)

        XCTAssertEqual(surface.resolvedProfileLinkURL(link)?.absoluteString, "https://github.com/octocat")
        XCTAssertNoThrow(try surface.inspect().find(text: "octocat"))
    }

    func testProfileLinkResolutionMatchesCanonicalBuiltInURLsAndFallsBack() {
        let cases: [(ProfileLinkType, String)] = [
            (.twitter, "https://x.com/jongle"),
            (.facebook, "https://facebook.com/jongle"),
            (.instagram, "https://instagram.com/jongle"),
            (.github, "https://github.com/jongle"),
            (.linkedin, "https://linkedin.com/in/jongle"),
            (.youtube, "https://youtube.com/@jongle"),
            (.tiktok, "https://tiktok.com/@jongle")
        ]

        for (linkType, expectedURL) in cases {
            XCTAssertEqual(
                linkType.resolvedURL(handle: "jongle", fallbackURL: "https://fallback.example")?.absoluteString,
                expectedURL
            )
        }
        XCTAssertEqual(
            ProfileLinkType.github.resolvedURL(handle: nil, fallbackURL: "https://fallback.example")?.absoluteString,
            "https://fallback.example"
        )
        XCTAssertEqual(
            ProfileLinkType.url.resolvedURL(handle: "ignored", fallbackURL: "https://example.test")?.absoluteString,
            "https://example.test"
        )
    }

    func testSurfaceRendersFullHeaderCountsAndPrimaryTabs() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        XCTAssertNoThrow(try surface.inspect().find(viewWithAccessibilityIdentifier: "public-profile-header"))
        XCTAssertNoThrow(try surface.inspect().find(text: "@alice"))
        XCTAssertNoThrow(try surface.inspect().find(text: "10 posts"))
        for title in ["Overview", "Posts", "Topics", "Friends", "Sources", "Communities"] {
            XCTAssertNoThrow(try surface.inspect().find(button: title))
        }
    }

    func testProfilePostCollectionHidesNegativeVoteCountsForRestrictedViewer() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.users.profile.posts.all"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/posts"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            hideDownCount: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        let controls = try surface.inspect().findAll(VoteControls.self)
        XCTAssertFalse(controls.isEmpty)
        XCTAssertTrue(try controls.allSatisfy { try $0.actualView().hideDownCount })
    }

    func testProfilePostCollectionDisablesHandlerlessVoteControls() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.users.profile.posts.all"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/posts"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            hideDownCount: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        let controls = try surface.inspect().findAll(VoteControls.self)
        XCTAssertFalse(controls.isEmpty)
        XCTAssertTrue(try controls.allSatisfy { control in
            try !control.actualView().canCreateVote
        })
    }

    func testProfileRendersTrustContextFromPeopleYouFollow() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        viewModel.userProfile.trustContext = try trustContextFixture()
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        let inspected = try surface.inspect()
        XCTAssertNoThrow(try inspected.find(viewWithAccessibilityIdentifier: "public-profile-positive-trust-context"))
        XCTAssertNoThrow(try inspected.find(text: "2 positive signals from people you follow"))
        XCTAssertNoThrow(try inspected.find(text: "positive-user"))
        XCTAssertNoThrow(try inspected.find(viewWithAccessibilityIdentifier: "public-profile-negative-trust-context"))
        XCTAssertNoThrow(try inspected.find(text: "1 negative signal from people you follow"))
        XCTAssertNoThrow(try inspected.find(text: "negative-user"))
    }

    func testPublicProfileTrustVoteRecoversSignedOutAndPreservesOfficialAndSelfRules() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match
        )
        await viewModel.load()
        let profile = try XCTUnwrap(viewModel.userProfile.header)
        var signInCount = 0
        let signedOut = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: false,
            turnstileSiteKey: nil,
            showSignIn: { signInCount += 1 },
            onNavigate: { _ in }
        )

        let signedOutControls = signedOut.trustControls(profile)
        XCTAssertNoThrow(try signedOutControls.inspect().find(VoteControls.self))
        try signedOutControls.inspect().find(button: "Sign In").tap()
        XCTAssertEqual(signInCount, 1)

        viewModel.userProfile.trustChoice = .like
        let officialControls = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            canCastPublicVotes: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        ).trustControls(profile)
        XCTAssertNoThrow(try officialControls.inspect().find(button: "Clear"))
        XCTAssertThrowsError(try officialControls.inspect().find(button: "Vouch"))

        viewModel.userProfile.trustVoteInFlight = true
        let inFlightControls = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            canCastPublicVotes: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        ).trustControls(profile)
        XCTAssertTrue(try inFlightControls.inspect().find(ViewType.Menu.self).isDisabled())
        XCTAssertThrowsError(try inFlightControls.inspect().find(button: "Clear"))

        viewModel.detailRelationIsSelfProfile = true
        XCTAssertThrowsError(try signedOut.trustControls(profile).inspect().find(VoteControls.self))
    }

    func testSourcesSurfaceRendersEveryContextualFilter() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            ApiFixtureLoader.data("native.users.profile.sources-following.article"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/user/alice/rss-feeds/following?feed_type=article"
        ))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        for title in ["All", "News", "Podcasts", "Videos"] {
            XCTAssertNoThrow(try surface.inspect().find(button: title))
        }
    }

    func testZeroCountPostAndFriendContextTabsHideUnlessSelected() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = try (zeroCountProfile(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            Data(#"{"results":[],"posts":{},"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#
                .utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/users/following"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8),
            200
        )

        let postSurface = try await surface(for: "/user/alice/reviews")
        XCTAssertNoThrow(try postSurface.inspect().find(button: "Reviews"))
        for hidden in ["All", "Discussions", "Comments"] {
            XCTAssertThrowsError(try postSurface.inspect().find(button: hidden))
        }

        let friendsSurface = try await surface(for: "/user/alice/users/following")
        XCTAssertNoThrow(try friendsSurface.inspect().find(button: "Following"))
        XCTAssertThrowsError(try friendsSurface.inspect().find(button: "Followers"))
    }

    func testRestrictedFixtureHidesNonSelectedTabsAndKeepsSelected404RouteVisible() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/restricted"] = (
            ApiFixtureLoader.data("native.users.profile.restricted"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-restricted/users/following"] = (
            Data(#"{"error":"User not found"}"#.utf8), 404
        )
        let route = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/user/restricted/users/following")
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        let profile = try XCTUnwrap(viewModel.userProfile.header)
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )
        let tabs = surface.visiblePrimaryTabs(profile)
        XCTAssertEqual(
            tabs.map(\.title),
            [.nativeSwiftProfileOverview, .nativeSwiftNavigationTitlesPosts, .nativeSwiftNavigationTitlesFriends]
        )
        XCTAssertTrue(tabs.first(where: { $0.title == .nativeSwiftNavigationTitlesFriends })?.selected == true)
        guard case .error = viewModel.state else {
            return XCTFail("Expected the restricted collection request to fail")
        }
    }

    func testFriendsPrimaryTabTargetsVisibleFollowersWhenFollowingIsHidden() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = try (
            profileWithFriendCounts(following: 0, followers: 2),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        let profile = try XCTUnwrap(viewModel.userProfile.header)
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: false,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )

        XCTAssertEqual(
            surface.visiblePrimaryTabs(profile).first { $0.title == .nativeSwiftNavigationTitlesFriends }?.path,
            "/user/alice/users/followers"
        )
    }

    func testEveryScopedEntityKindRendersItsCanonicalTypedRow() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        let fixtures = [
            ("/api/v1/posts", "native.users.profile.posts.all"),
            ("/api/v1/users/user-abc/topics/following", "native.users.profile.topics-following.first-page"),
            ("/api/v1/users/user-abc/users/following", "swift.users.following.default"),
            ("/api/v1/users/user-abc/rss-feeds/following", "native.users.profile.sources-following.article"),
            ("/api/v1/users/user-abc/communities/member", "native.users.profile.communities-member.first-page")
        ]
        for (endpoint, fixture) in fixtures {
            CannedFeedURLProtocol.handlers[endpoint] = (ApiFixtureLoader.data(fixture), 200)
        }
        let cases = [
            ("/user/alice/posts", "public-profile-post-row"),
            ("/user/alice/topics/following", "public-profile-topic-row"),
            ("/user/alice/users/following", "public-profile-user-row"),
            ("/user/alice/rss-feeds/following", "public-profile-source-row"),
            ("/user/alice/communities/member", "public-profile-community-row")
        ]

        for (path, identifier) in cases {
            let loadedSurface = try await surface(for: path)
            XCTAssertNoThrow(
                try loadedSurface.inspect().find(viewWithAccessibilityIdentifier: identifier),
                path
            )
        }
    }

    private func surface(for path: String) async throws -> NativeUserProfileSurface {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match
        )
        await viewModel.load()
        return NativeUserProfileSurface(
            entry: route.entry,
            viewModel: viewModel,
            isSignedIn: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )
    }

    private func zeroCountProfile() throws -> Data {
        try profileWithFriendCounts(following: 0, followers: 0, zeroOtherCounts: true)
    }

    private func trustContextFixture() throws -> UserTrustContext {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(
            UserTrustContext.self,
            from: Data(
                #"""
                {
                  "positive_by_following": {
                    "total": 2,
                    "users": [{ "id": "positive-user", "username": "positive-user" }]
                  },
                  "negative_by_following": {
                    "total": 1,
                    "users": [{ "id": "negative-user", "username": "negative-user" }]
                  },
                  "election_vote": null
                }
                """#.utf8
            )
        )
    }

    private func profileHeaderFixture(
        displayAccountName: String? = nil,
        verifiedDisplayName: String? = nil,
        profileLinkType: String = "url",
        profileLinkHandle: String? = nil,
        profileLinkURL: String = "https://example.test"
    ) throws -> Data {
        var root = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data("native.users.profile.default"))
                as? [String: Any]
        )
        var user = try XCTUnwrap(root["user"] as? [String: Any])
        user["display_account"] = displayAccountName.map { ["id": "display-1", "name": $0] }
            ?? NSNull()
        user["verified_display_name"] = verifiedDisplayName ?? NSNull()
        root["user"] = user
        var links = try XCTUnwrap(root["profile_links"] as? [[String: Any]])
        links[0]["link_type"] = profileLinkType
        links[0]["handle"] = profileLinkHandle ?? NSNull()
        links[0]["name"] = NSNull()
        links[0]["url"] = profileLinkURL
        root["profile_links"] = links
        return try JSONSerialization.data(withJSONObject: root)
    }

    private func profileWithFriendCounts(
        following: Int,
        followers: Int,
        zeroOtherCounts: Bool = false
    ) throws -> Data {
        var root = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data("native.users.profile.default"))
                as? [String: Any]
        )
        var metrics = try XCTUnwrap(root["user_metrics"] as? [String: Any])
        var counts = try XCTUnwrap(metrics["count"] as? [String: Any])
        counts["users_following"] = following
        counts["users_followers"] = followers
        if zeroOtherCounts {
            for key in [
                "reviews", "discussions", "comments", "topics_following",
                "rss_feeds_following", "communities_member"
            ] {
                counts[key] = 0
            }
        }
        metrics["count"] = counts
        metrics["viewer_count"] = [String: Int]()
        root["user_metrics"] = metrics
        return try JSONSerialization.data(withJSONObject: root)
    }
}

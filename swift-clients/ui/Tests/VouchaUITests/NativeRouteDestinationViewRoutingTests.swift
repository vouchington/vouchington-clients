import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteDestinationViewRoutingTests: NativeRouteSurfaceViewModelTestCase {
    func testDestinationDefaultsToDenyPublicVotes() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertTrue(sut.routeIdentity.contains("|false|"))
    }

    func testDestinationIdentityChangesWhenNegativeVoteCountVisibilityChanges() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages"))
        let unrestricted = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            hideDownCount: false
        )
        let restricted = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            hideDownCount: true
        )

        XCTAssertNotEqual(unrestricted.routeIdentity, restricted.routeIdentity)
    }

    func testFocusedRssRoutePassesSignedInViewerToFocusedSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/news"))
        let sut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            routeQuery: "rss_item=item-1",
            currentUserId: "viewer-1"
        )
        let focusedSurface = try sut.inspect().find(NativeFocusedRssFeedItemSurface.self).actualView()

        XCTAssertEqual(focusedSurface.currentUserId, "viewer-1")
    }

    private let turnstileSiteKey = "test-site-key"

    func testLandingPagesOwnerRouteUsesManagementSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/landing-pages"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(button: "Create landing page"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Public routes"))
    }

    func testLandingPagesPublicRouteUsesListSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/home"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertEqual(try sut.inspect().find(text: "Landing pages").string(), "Landing pages")
        XCTAssertEqual(try sut.inspect().find(text: "Public routes").string(), "Public routes")
        XCTAssertThrowsError(try sut.inspect().find(button: "Create landing page"))
    }

    func testLandingPageSlugNamedAnalyticsUsesManagementSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/landing-page/analytics"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(button: "Create landing page"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Public routes"))
    }

    func testTopicRecommendationsDefaultRouteUsesListSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic-recommendations"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertEqual(try sut.inspect().find(text: "Recommendations").string(), "Recommendations")
        XCTAssertNoThrow(try sut.inspect().find(text: "Create/edit"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Submit Recommendation"))
    }

    func testNotificationSettingsRouteUsesFocusedSettingsSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/notification-settings"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(text: "Notifications"))
        XCTAssertNoThrow(try sut.inspect().find(viewWithAccessibilityIdentifier: "notification-settings-heading"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Save Notifications"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Account"))
        XCTAssertThrowsError(try sut.inspect().find(text: "API Keys"))
    }

    func testSettingsDirectoryNavigationEntryTargetsFocusedNotificationSettingsRoute() throws {
        let advancedGroup = try XCTUnwrap(
            AppSection.settings.nativeParityGroups.first { uiEnglish($0.title) == "Advanced" }
        )
        let entry = try XCTUnwrap(
            advancedGroup.entries.first { $0.destinationIdentifier == .notificationSettings }
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: entry.representativePath))
        let sut = NativeRouteDestinationView(entry: entry, routeMatch: route.match)

        XCTAssertEqual(entry.representativePath, "/my/notification-settings")
        XCTAssertNoThrow(try sut.inspect().find(text: "Notifications"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Account"))
    }

    func testTopicRecommendationsCreateAndEditRoutesUseFormSurface() throws {
        let createRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/create"))
        let create = NativeRouteDestinationView(
            entry: createRoute.entry,
            routeMatch: createRoute.match,
            turnstileSiteKey: turnstileSiteKey
        )
        XCTAssertNoThrow(try create.inspect().find(button: "Submit recommendation"))
        XCTAssertNoThrow(try create.inspect().find(button: "Verify"))

        let editRoute = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/topic-rec-1/edit")
        )
        let edit = NativeRouteDestinationView(
            entry: editRoute.entry,
            routeMatch: editRoute.match,
            turnstileSiteKey: turnstileSiteKey
        )
        XCTAssertNoThrow(try edit.inspect().find(button: "Save recommendation"))
        XCTAssertThrowsError(try edit.inspect().find(button: "Verify"))
    }

    func testTopicRecommendationPublicDetailRouteUsesNativeRecommendationSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/topic-rec-1"))
        let detail = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertNoThrow(try detail.inspect().find(NativeTopicRecommendationSurface.self))
    }

    func testAdminPostComposeRouteUsesAdminOnlyComposeTypes() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/articles/create"))
        let sut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: true,
            turnstileSiteKey: turnstileSiteKey
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "Article"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Blog post"))
    }

    func testAdminPostComposeRouteRequiresSignedInAdministrator() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/blog/create"))
        let signedOut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false,
            isAdministrator: false
        )
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try signedOut.inspect().find(text: "Discussion"))

        let nonAdmin = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: false
        )
        XCTAssertNoThrow(try nonAdmin.inspect().find(text: "Administrator required"))
        XCTAssertThrowsError(try nonAdmin.inspect().find(text: "Discussion"))
    }

    func testMembershipGrantRouteUsesDedicatedSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/memberships/grants"))
        let allowed = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true,
            isAdministrator: true
        )
        XCTAssertNoThrow(try allowed.inspect().find(text: "Membership grants"))
        XCTAssertThrowsError(try allowed.inspect().find(NativeListSurface.self))
    }

    func testCommunityDetailRouteUsesCommunitySurfaceAndPostCreateStaysCompose() throws {
        let detailRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/builders"))
        let detail = NativeRouteDestinationView(
            entry: detailRoute.entry,
            routeMatch: detailRoute.match,
            turnstileSiteKey: turnstileSiteKey
        )
        XCTAssertNoThrow(try detail.inspect().find(button: "Join"))
        XCTAssertThrowsError(try detail.inspect().find(text: "Community feeds"))

        let composeRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/builders/posts/create"))
        let compose = NativeRouteDestinationView(
            entry: composeRoute.entry,
            routeMatch: composeRoute.match,
            turnstileSiteKey: turnstileSiteKey
        )
        XCTAssertEqual(composeRoute.entry.destinationIdentifier, .postCompose)
        XCTAssertNoThrow(try compose.inspect().find(button: "Publish"))
    }

    func testCommunityDetailRouteAcceptsSiteModeratorState() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/builders/modmail"))
        let sut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSiteModerator: true,
            turnstileSiteKey: turnstileSiteKey
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Open"))
    }

    func testRssFeedItemTagRouteUsesTagManagementSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/rss-feed-items/item-1/tags/topic"))

        XCTAssertEqual(route.entry.destinationIdentifier, .rssFeedItemDetail)
        XCTAssertEqual(route.match.param("id"), "item-1")
        XCTAssertEqual(route.match.param("objectType"), "topic")

        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(text: "Manage tags"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Details"))
    }

    func testMessagesRouteUsesDirectMessagesSurfaceWhenClientExists() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/new"))
        let sut = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            currentUserId: "user-1"
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "Recipients"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Thread"))
    }

    func testMessagesRouteUsesDirectMessagesSurfaceOutsideScrollViewWhenSignedIn() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/new"))
        let sut = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            currentUserId: "user-1"
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "Recipients"))
        XCTAssertThrowsError(try sut.inspect().find(ViewType.ScrollView.self))
    }

    func testMessagesModmailRouteUsesListSurfaceInsteadOfDirectMessagesSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/modmail/example/thread-1"))
        let sut = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            currentUserId: "user-1"
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "Messages"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Recipients"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Participants"))
    }

    func testMessagesRouteFallsBackWhenSignedOutEvenWithClient() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages"))
        let sut = try NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isSignedIn: false,
            showSignIn: {}
        )

        XCTAssertEqual(try sut.inspect().find(text: "Messages").string(), "Messages")
        XCTAssertEqual(try sut.inspect().find(text: "Thread").string(), "Thread")
        XCTAssertThrowsError(try sut.inspect().find(text: "Recipients"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Participants"))
    }

    func testMessagesRouteFallsBackToListSurfaceWithoutClient() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertEqual(try sut.inspect().find(text: "Messages").string(), "Messages")
        XCTAssertEqual(try sut.inspect().find(text: "Thread").string(), "Thread")
        XCTAssertThrowsError(try sut.inspect().find(text: "Recipients"))
    }

    func testChatRouteUsesNativeConversationSurface() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/chat"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match)

        XCTAssertNoThrow(try sut.inspect().find(text: "Chats"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Send"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Stop"))
    }

    func testChatRouteRequiresSignIn() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/chat"))
        let sut = NativeRouteDestinationView(entry: route.entry, routeMatch: route.match, isSignedIn: false)

        XCTAssertNoThrow(try sut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Send"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Stop"))
    }

    func testDedicatedStaffAppealsRouteNeverRunsTheGenericLoader() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/appeals"))
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ModerationAppealsTestSupport.list([ModerationAppealsTestSupport.appeal()]), 200
        )

        for role in [(administrator: true, moderator: false), (administrator: false, moderator: false)] {
            let surface = try NativeRouteDestinationSurface(
                entry: route.entry,
                client: makeClient(),
                routeMatch: route.match,
                routeQuery: nil,
                isSignedIn: true,
                isAdministrator: role.administrator,
                isSiteModerator: role.moderator,
                showSignIn: {}
            )
            XCTAssertTrue(surface.isDedicatedStaffAppealsRoute)
            XCTAssertFalse(surface.shouldLoadRouteSurfaceContent)
            if surface.shouldLoadRouteSurfaceContent {
                await surface.viewModel.load()
            }
        }

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testAppealsRouteIdentityRecreatesStaffSurfaceWhenSigningInWithoutUserId() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/appeals"))
        let signedOut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false,
            currentUserId: nil,
            isAdministrator: true
        )
        let signedIn = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true,
            currentUserId: nil,
            isAdministrator: true
        )

        XCTAssertNotEqual(signedOut.routeIdentity, signedIn.routeIdentity)
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try signedIn.inspect().find(text: "Pending"))
    }
}

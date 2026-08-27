import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunitySurfaceViewTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunitySurfaceRendersBrowseCreateInviteAndDetailModes() throws {
        let browse = CommunitySurface(client: nil, routeMatch: nil, turnstileSiteKey: "site-key")
        XCTAssertNoThrow(try browse.inspect().find(ViewType.TextField.self))
        XCTAssertNoThrow(try browse.inspect().find(button: "Search"))

        let createRoute = NativeRouteMatch(path: "/communities/create", template: "/communities/create")
        let create = CommunitySurface(
            client: nil, routeMatch: createRoute, turnstileSiteKey: "site-key"
        )
        XCTAssertNoThrow(try create.inspect().find(button: "Verify"))
        XCTAssertNoThrow(try create.inspect().find(button: "Create Community"))

        let signedOutCreate = CommunitySurface(
            client: nil,
            routeMatch: createRoute,
            isSignedIn: false,
            turnstileSiteKey: "site-key",
            showSignIn: {}
        )
        XCTAssertEqual(try signedOutCreate.inspect().find(text: "Sign in required").string(), "Sign in required")
        XCTAssertNoThrow(try signedOutCreate.inspect().find(button: "Sign in"))

        let inviteRoute = NativeRouteMatch(
            path: "/communities/invite/code-1",
            template: "/communities/invite/:code",
            params: ["code": "code-1"]
        )
        let invite = CommunitySurface(
            client: nil, routeMatch: inviteRoute, turnstileSiteKey: "site-key"
        )
        XCTAssertEqual(try invite.inspect().find(text: "Community invite").string(), "Community invite")

        let detailRoute = NativeRouteMatch(
            path: "/communities/builders/apply",
            template: "/communities/:slug/apply",
            params: ["slug": "builders"]
        )
        let detail = CommunitySurface(
            client: nil, routeMatch: detailRoute, turnstileSiteKey: "site-key"
        )
        XCTAssertNoThrow(try detail.inspect().find(button: "Join"))
        XCTAssertNoThrow(try detail.inspect().find(button: "Apply"))
    }

    func testCommunitySubviewsRenderRowsStatusAndTabControls() throws {
        let browseViewModel = CommunityBrowseViewModel(client: nil)
        let browse = CommunityBrowseSurface(viewModel: browseViewModel)
        XCTAssertNoThrow(try browse.inspect().find(button: "Search"))

        let createViewModel = CommunityCreateViewModel(client: nil, appAttestationService: nil)
        let create = CommunityCreateSurface(viewModel: createViewModel, turnstileSiteKey: "site-key")
        XCTAssertNoThrow(try create.inspect().find(button: "Verify"))
        XCTAssertNoThrow(try create.inspect().find(button: "Create Community"))

        let detailViewModel = CommunityDetailViewModel(
            client: nil, slug: "builders", initialTab: .applications
        )
        detailViewModel.applicationMessage = "Let me in"
        let detail = CommunityWorkspaceSurface(viewModel: detailViewModel)
        XCTAssertNoThrow(try detail.inspect().find(button: "Join"))
        XCTAssertNoThrow(try detail.inspect().find(button: "Apply"))

        detailViewModel.selectedTab = .invites
        detailViewModel.inviteRecipient = "person@example.com"
        let invites = CommunityWorkspaceSurface(viewModel: detailViewModel)
        XCTAssertNoThrow(try invites.inspect().find(button: "Invite"))

        let invite = CommunityInviteRedemptionSurface(
            viewModel: CommunityInviteRedemptionViewModel(client: nil, code: "code-1")
        )
        XCTAssertEqual(try invite.inspect().find(text: "Community invite").string(), "Community invite")

        XCTAssertEqual(
            try stateText(.requiredTurnstile).inspect().find(text: "Verification required.").string(),
            "Verification required."
        )
        XCTAssertEqual(
            try stateText(.error(UiMessage(.nativeSwiftCommunityStatusUnableToLoadCommunity)))
                .inspect().find(text: "Unable to load community.").string(),
            "Unable to load community."
        )
        XCTAssertNoThrow(try stateText(.loaded).inspect())
    }

    func testSignedOutCommunityActionsPromptSignIn() throws {
        var signInCount = 0
        let detailViewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .applications
        )
        let detail = CommunityWorkspaceSurface(
            viewModel: detailViewModel,
            isSignedIn: false,
            showSignIn: { signInCount += 1 }
        )

        try detail.inspect().find(button: "Join").tap()
        try detail.inspect().find(button: "Apply").tap()

        XCTAssertEqual(signInCount, 2)
    }

    func testSignedOutInviteRedemptionPromptsSignIn() throws {
        var signInCount = 0
        let invite = CommunityInviteRedemptionSurface(
            viewModel: CommunityInviteRedemptionViewModel(client: nil, code: "code-1"),
            isSignedIn: false,
            showSignIn: { signInCount += 1 }
        )

        try invite.inspect().find(button: "Redeem Invite").tap()

        XCTAssertEqual(signInCount, 1)
    }
}

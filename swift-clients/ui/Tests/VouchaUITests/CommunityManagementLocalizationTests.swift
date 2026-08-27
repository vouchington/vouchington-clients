import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityManagementLocalizationTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityTabTitlesCoverEveryLocalizedDestination() {
        let expectedTitles = [
            "Posts", "News", "Members", "Lists", "List Topics", "List Sources", "List Posts",
            "List Domains", "List URLs", "Pinned", "Applications", "Invites", "Settings",
            "Moderation", "Modlog", "Modmail", "Vacation", "Bans", "Restrictions", "Analytics"
        ]

        XCTAssertEqual(CommunitySurfaceTab.allCases.map { uiEnglish($0.titleKey) }, expectedTitles)
        XCTAssertEqual(CommunitySurfaceTab.allCases.map(\.id), CommunitySurfaceTab.allCases.map(\.rawValue))
    }

    func testSignedOutCommunityManagementControlsRouteEveryActionToSignIn() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation,
            isAdministrator: true,
            modmailThreadId: "thread-1"
        )
        var signInCount = 0
        let showSignIn = { signInCount += 1 }

        try CommunityAgentAndAutomodControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: showSignIn
        ).inspect().find(button: "Enable").tap()
        try CommunityApplicationReviewControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: showSignIn
        ).inspect().find(button: "Approve").tap()
        try CommunityApplicationReviewControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: showSignIn
        ).inspect().find(button: "Reject").tap()
        try CommunityInviteManagementControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: showSignIn
        ).inspect().find(button: "Revoke invite").tap()
        try CommunityModerationAdminControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: showSignIn
        ).inspect().find(button: "Approve").tap()
        try CommunityModmailControls(
            viewModel: viewModel,
            isSignedIn: false,
            canModerate: true,
            showSignIn: showSignIn
        ).inspect().find(button: "Send").tap()
        try CommunityVacationControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: showSignIn
        ).inspect().find(button: "Set vacation").tap()
        try CommunityVacationControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: showSignIn
        ).inspect().find(button: "Clear vacation").tap()

        XCTAssertEqual(signInCount, 8)
    }
}

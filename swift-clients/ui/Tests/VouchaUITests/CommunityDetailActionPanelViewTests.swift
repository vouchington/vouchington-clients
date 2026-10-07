import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityDetailActionPanelViewTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityWorkspaceSurfaceShowsMemberListApplicationInviteAndPinnedControls() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .members,
            isAdministrator: true
        )

        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Update role"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Remove"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect()
            .find(button: "Transfer ownership"))

        viewModel.selectedTab = .listTopics
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Add"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Remove"))

        viewModel.selectedTab = .applications
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Approve"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Reject"))

        viewModel.selectedTab = .invites
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Revoke invite"))

        viewModel.selectedTab = .pinnedPosts
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect()
            .find(button: "Update pinned posts"))
    }

    func testCommunityWorkspaceSurfaceShowsModerationAgentAutomodAndVacationControls() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation,
            isAdministrator: true
        )

        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Approve"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Unpublish"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Escalate"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Ban"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Lift ban"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Activate"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Enable"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Create"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "False positive"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Simulate"))

        viewModel.selectedTab = .settings
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Set vacation"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Clear vacation"))

        viewModel.selectedTab = .modmail
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Send"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Resolve"))
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Reopen"))
        XCTAssertThrowsError(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Enable"))
    }

    func testVacationDigestToggleRequiresSignIn() throws {
        let viewModel = CommunityDetailViewModel(client: nil, slug: "builders")
        var requestedSignIn = false
        let controls = CommunityVacationControls(
            viewModel: viewModel,
            isSignedIn: false,
            showSignIn: { requestedSignIn = true }
        )

        try controls.inspect().find(ViewType.Toggle.self).tap()

        XCTAssertTrue(requestedSignIn)
        XCTAssertFalse(viewModel.suppressCommunityDigestsWhileOnVacation)
    }

    func testVacationDigestToggleSendsPreferenceUpdateWhenSignedIn() async throws {
        let updateCompleted = expectation(description: "Digest preference update completed")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderator-vacation"] = (
            Data(#"{"should_suppress_community_digests_while_on_vacation":true}"#.utf8),
            200
        )
        let viewModel = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        let controls = CommunityVacationControls(
            viewModel: viewModel,
            isSignedIn: true,
            showSignIn: {},
            digestPreferenceUpdateCompleted: { updateCompleted.fulfill() }
        )

        try controls.inspect().find(ViewType.Toggle.self).tap()
        await fulfillment(of: [updateCompleted], timeout: 1)

        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains {
            $0?.contains(#""should_suppress_community_digests_while_on_vacation":true"#) == true
        })
    }
}

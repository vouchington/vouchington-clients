import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityDetailActionPanelCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityDetailActionPanelCoversMemberListApplicationInviteAndPinnedControls() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .members,
            isAdministrator: true
        )

        viewModel.selectedTab = .members
        try tapAllButtons(in: CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {}))

        viewModel.selectedTab = .listTopics
        try tapAllButtons(in: CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {}))

        viewModel.selectedTab = .applications
        try tapAllButtons(in: CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {}))

        viewModel.selectedTab = .invites
        try tapAllButtons(in: CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {}))

        viewModel.selectedTab = .pinnedPosts
        try tapAllButtons(in: CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {}))
    }

    func testCommunityDetailActionPanelCoversModerationAgentAutomodVacationAndModmailControls() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation,
            isAdministrator: true,
            modmailThreadId: "thread-1"
        )

        viewModel.selectedTab = .moderation
        let moderationButtons = try buttons(in: CommunityDetailActionPanel(
            viewModel: viewModel,
            isSignedIn: true,
            showSignIn: {}
        ))
        XCTAssertEqual(moderationButtons.count, 28)
        XCTAssertNoThrow(try CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            .inspect()
            .find(button: "Update"))
        XCTAssertNoThrow(try CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            .inspect()
            .find(button: "Load recent"))
        for button in moderationButtons {
            try button.tap()
        }

        viewModel.selectedTab = .modmail
        let modmailButtons = try buttons(in: CommunityDetailActionPanel(
            viewModel: viewModel,
            isSignedIn: true,
            showSignIn: {}
        ))
        XCTAssertEqual(modmailButtons.count, 4)
        XCTAssertNoThrow(try CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            .inspect()
            .find(button: "Open"))
        for button in modmailButtons {
            try button.tap()
        }
    }

    func testCommunityDetailActionPanelHidesModeratorOnlyModmailControlsForMembers() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .modmail,
            modmailThreadId: "thread-1"
        )

        let modmailButtons = try buttons(in: CommunityDetailActionPanel(
            viewModel: viewModel,
            isSignedIn: true,
            showSignIn: {}
        ))

        XCTAssertEqual(modmailButtons.count, 2)
        XCTAssertNoThrow(try CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            .inspect()
            .find(button: "Open"))
        XCTAssertNoThrow(try CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            .inspect()
            .find(button: "Send"))
        XCTAssertThrowsError(try CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            .inspect()
            .find(button: "Resolve"))
        XCTAssertThrowsError(try CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            .inspect()
            .find(button: "Reopen"))
    }

    func testCommunityWorkspaceSurfaceBuildsActionPanelContent() throws {
        let viewModel = CommunityDetailViewModel(
            client: nil,
            slug: "builders",
            initialTab: .moderation,
            isAdministrator: true,
            modmailThreadId: "thread-1"
        )
        let workspace = CommunityWorkspaceSurface(viewModel: viewModel)

        XCTAssertNoThrow(try workspace.inspect().find(button: "Enable"))
        XCTAssertNoThrow(try workspace.inspect().find(button: "Set vacation"))

        viewModel.selectedTab = .modmail
        XCTAssertNoThrow(try CommunityWorkspaceSurface(viewModel: viewModel).inspect().find(button: "Send"))
    }

    private func tapAllButtons(in view: some View) throws {
        let buttons = try buttons(in: view)
        for button in buttons {
            try button.tap()
        }
    }

    private func buttons(in view: some View) throws -> [InspectableView<ViewType.Button>] {
        try view.inspect().findAll(ViewType.Button.self)
    }
}

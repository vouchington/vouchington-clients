import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class CommunityInviteLifecycleTests: NativeRouteSurfaceViewModelTestCase {
    func testDisappearanceInvalidatesDelayedInviteCreation() async throws {
        let path = "/api/v1/communities/builders/invites"
        CannedFeedURLProtocol.handlers[path] = (createdInviteResponse(code: "late-code"), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders", initialTab: .invites)
        viewModel.inviteRecipient = "alice"
        let invite = Task { await viewModel.invite() }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: path, method: "POST").wait()

        let surface = CommunityWorkspaceSurface(viewModel: viewModel)
        try surface.inspect().scrollView().callOnDisappear()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await invite.value

        XCTAssertNil(viewModel.oneTimeInviteCode)
        XCTAssertThrowsError(try CommunityTabControls(
            viewModel: viewModel,
            isSignedIn: true,
            showSignIn: {}
        ).inspect().find(text: "late-code"))
    }

    func testRenderedDismissControlClearsTheOneTimeCode() throws {
        let viewModel = CommunityDetailViewModel(client: nil, slug: "builders", initialTab: .invites)
        viewModel.showInviteCode("one-time-code")
        let controls = CommunityTabControls(viewModel: viewModel, isSignedIn: true, showSignIn: {})
        let inspection = try controls.inspect()
        XCTAssertEqual(try inspection.find(text: "one-time-code").string(), "one-time-code")

        try inspection.find(button: UiMessages.string(.nativeSwiftCommunitiesDismissInviteCode, locale: .english)).tap()

        XCTAssertNil(viewModel.oneTimeInviteCode)
        XCTAssertThrowsError(try controls.inspect().find(text: "one-time-code"))
    }

    private func createdInviteResponse(code: String) -> Data {
        Data(
            """
            {
              "community_invite": {
                "id": "invite-1",
                "community_id": "community-1",
                "code": "\(code)",
                "invited_user_id": null,
                "invited_email": null,
                "invited_by_id": "moderator-1",
                "accepted_at": null,
                "accepted_by_id": null,
                "declined_at": null,
                "revoked_at": null,
                "created_at": "2026-07-01T00:00:00Z"
              }
            }
            """.utf8
        )
    }
}

import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CommunityAutomodActionVisibilityTests: NativeRouteSurfaceViewModelTestCase {
    func testModeratorCanInspectEachCurrentCommunityAutomodAction() async throws {
        for (action, title) in [
            ("record_only", "Record only"), ("review_queue", "Send to review queue"), ("unpublish", "Unpublish")
        ] {
            let detail = try await communityDetail(action: action, role: "moderator")
            let viewModel = CommunityDetailViewModel(client: nil, slug: "test-community", initialTab: .moderation)
            viewModel.communityDetail = detail

            let panel = CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
            XCTAssertNoThrow(try panel.inspect().find(text: title))
        }
    }

    func testModeratorCanOpenAutomodFlagReview() async throws {
        let detail = try await communityDetail(action: "review_queue", role: "moderator")
        let viewModel = CommunityDetailViewModel(client: nil, slug: "test-community", initialTab: .moderation)
        viewModel.communityDetail = detail
        let panel = CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
        XCTAssertNoThrow(try panel.inspect().find(text: "Automod flags"))
    }

    func testOwnerCanChooseAutomodActionInCommunitySettings() async throws {
        let detail = try await communityDetail(action: "review_queue", role: "owner")
        let viewModel = CommunityDetailViewModel(client: nil, slug: "test-community", initialTab: .settings)
        viewModel.communityDetail = detail
        let panel = CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
        XCTAssertNoThrow(try panel.inspect().find(button: "Save automod action"))
        XCTAssertThrowsError(try panel.inspect().find(text: "Automod flags"))
    }

    func testMemberCannotInspectCommunityAutomodAction() async throws {
        let detail = try await communityDetail(action: "review_queue", role: "member")
        let viewModel = CommunityDetailViewModel(client: nil, slug: "test-community", initialTab: .moderation)
        viewModel.communityDetail = detail

        let panel = CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
        XCTAssertThrowsError(try panel.inspect().find(text: "Automod flags"))
    }

    private func communityDetail(action: String, role: String) async throws -> CommunityResponse {
        var response = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data("web.communities.automod-settings.update.default"))
                as? [String: Any]
        )
        var community = try XCTUnwrap(response["community"] as? [String: Any])
        community["automod_action"] = action
        response["community"] = community
        response["membership"] = [
            "id": "membership-1",
            "community_id": "community-1",
            "user_id": "user-1",
            "role": role,
            "approved_by_id": NSNull(),
            "created_at": "2026-07-01T00:00:00Z",
            "updated_at": "2026-07-01T00:00:00Z",
            "removed_at": NSNull(),
            "removed_by_id": NSNull()
        ]
        let encodedResponse = try JSONSerialization.data(withJSONObject: response)
        CannedFeedURLProtocol.handlers["/api/v1/communities/test-community"] = (encodedResponse, 200)
        let client = try makeClient()
        return try await client.send(.community(idOrSlug: "test-community"))
    }
}

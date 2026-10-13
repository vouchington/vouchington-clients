import Foundation
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityMemberAvatarRenderingTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityMemberRowsUseCanonicalPlacementAndIgnoreLegacyImageIds() async throws {
        let fixture = ApiFixtureLoader.data("web.communities.members.default")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = (fixture, 200)
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .members
        )

        let rows = try await viewModel.loadRows(client: makeClient())
        let member = try XCTUnwrap(rows.first)
        XCTAssertEqual(member.title, "testuser")
        XCTAssertEqual(member.detail, "Owner")
        XCTAssertTrue(try XCTUnwrap(member.avatarURL).contains(
            "/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96"
        ))
        XCTAssertNoThrow(try NativeSurfaceRow(row: member).inspect().find(
            viewWithAccessibilityIdentifier: "native-surface-avatar"
        ))

        var legacyOnly = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        var users = try XCTUnwrap(legacyOnly["users"] as? [String: [String: Any]])
        users["user-1"]?.removeValue(forKey: "profile_image_placement")
        users["user-1"]?["profile_image_id"] = "00000000-0000-7000-8000-000000000201"
        legacyOnly["users"] = users
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = try (
            JSONSerialization.data(withJSONObject: legacyOnly), 200
        )
        let legacyRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertNil(try XCTUnwrap(legacyRows.first).avatarURL)
    }
}

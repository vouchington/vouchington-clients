import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityMemberAvatarRenderingTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityMemberRowsUseCanonicalPlacementAndIgnoreLegacyImageIds() async throws {
        let fixture = ApiFixtureLoader.data("web.communities.members.default")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = (fixture, 200)
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                imageBaseURL: XCTUnwrap(URL(string: "https://images.custom.test")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let viewModel = CommunityDetailViewModel(
            client: client,
            slug: "builders",
            initialTab: .members
        )

        let rows = try await viewModel.loadRows(client: client)
        let member = try XCTUnwrap(rows.first)
        XCTAssertEqual(member.title, "testuser")
        XCTAssertEqual(member.detail, "Owner")
        XCTAssertEqual(
            "https://images.custom.test/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96",
            member.avatarURL
        )
        let avatar = try NativeSurfaceRow(row: member).inspect().find(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(try avatar.actualView().resolvedURLString, member.avatarURL)
        XCTAssertTrue(try avatar.accessibilityHidden())

        var legacyOnly = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        var users = try XCTUnwrap(legacyOnly["users"] as? [String: [String: Any]])
        users["user-1"]?.removeValue(forKey: "profile_image_placement")
        users["user-1"]?["profile_image_id"] = "00000000-0000-7000-8000-000000000201"
        legacyOnly["users"] = users
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = try (
            JSONSerialization.data(withJSONObject: legacyOnly), 200
        )
        let legacyRows = try await viewModel.loadRows(client: client)
        XCTAssertNil(try XCTUnwrap(legacyRows.first).avatarURL)
    }
}

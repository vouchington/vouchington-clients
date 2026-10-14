import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityModeratorStatsAvatarTests: NativeRouteSurfaceViewModelTestCase {
    func testModeratorStatsRowsRenderFixtureDerivedCanonicalAvatar() async throws {
        try registerModerationResponses(stats: moderatorStatsFixtureWithMemberPlacement())
        let client = try makeImageHostClient()
        let viewModel = CommunityDetailViewModel(
            client: client,
            slug: "builders",
            initialTab: .moderation
        )

        let rows = try await viewModel.loadRows(client: client)

        let stat = try XCTUnwrap(rows.first { $0.id == "user-1" })
        XCTAssertEqual(stat.title, "testuser")
        XCTAssertEqual(stat.detail, "3")
        XCTAssertTrue(try XCTUnwrap(stat.avatarURL).hasPrefix("https://images.custom.test/"))
        XCTAssertTrue(try XCTUnwrap(stat.avatarURL).contains(
            "/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96"
        ))
        let rendered = try NativeSurfaceRow(row: stat).inspect()
        XCTAssertNoThrow(try rendered.find(
            viewWithAccessibilityIdentifier: "native-surface-avatar"
        ))
        let image = try rendered.find(ViewType.View<AsyncImageView>.self).actualView()
        XCTAssertEqual(image.resolvedURLString, stat.avatarURL)
        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(\.path)), Set([
            "/api/v1/communities/builders/moderator-stats",
            "/api/v1/communities/builders/posts/pending",
            "/api/v1/communities/builders/reports/pending"
        ]))
    }

    func testMissingStatsAvatarKeepsUsernameAndPendingQueueRows() async throws {
        registerModerationResponses(stats: ApiFixtureLoader.data("web.communities.moderator-stats.default"))
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )

        let rows = try await viewModel.loadRows(client: makeClient())

        let stat = try XCTUnwrap(rows.first { $0.id == "user-1" })
        XCTAssertEqual(stat.title, "testuser")
        XCTAssertNil(stat.avatarURL)
        XCTAssertTrue(rows.contains { $0.title == "Pending posts" })
        XCTAssertTrue(rows.contains { $0.title == "Pending reports" })
    }

    func testMissingModeratorUserUsesShortActorFallbackWithoutAvatar() async throws {
        try registerModerationResponses(stats: moderatorStatsFixtureWithoutUser())
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )

        let rows = try await viewModel.loadRows(client: makeClient())

        let stat = try XCTUnwrap(rows.first { $0.id == "00000000-0000-7000-8000-000000000499" })
        XCTAssertEqual(stat.title, "00000000")
        XCTAssertNil(stat.avatarURL)
        XCTAssertTrue(rows.contains { $0.title == "Pending posts" })
        XCTAssertTrue(rows.contains { $0.title == "Pending reports" })
    }

    func testForbiddenStatsStillLoadsPendingQueueRows() async throws {
        registerModerationResponses(stats: Data(#"{"error":"forbidden"}"#.utf8), statsStatus: 403)
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )

        let rows = try await viewModel.loadRows(client: makeClient())

        XCTAssertFalse(rows.contains { $0.id == "user-1" })
        XCTAssertTrue(rows.contains { $0.title == "Pending posts" })
        XCTAssertTrue(rows.contains { $0.title == "Pending reports" })
    }

    private func registerModerationResponses(stats: Data, statsStatus: Int = 200) {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderator-stats"] = (stats, statsStatus)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts/pending"] = (
            Data(#"{"results":[],"posts":{},"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/reports/pending"] = (
            ApiFixtureLoader.data("native.community.pending-reports.paginated"),
            200
        )
    }

    private func makeImageHostClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                imageBaseURL: XCTUnwrap(URL(string: "https://images.custom.test")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func moderatorStatsFixtureWithMemberPlacement() throws -> Data {
        var stats = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.moderator-stats.default")
        ) as? [String: Any])
        let members = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.members.default")
        ) as? [String: Any])
        let memberUsers = try XCTUnwrap(members["users"] as? [String: [String: Any]])
        let placement = try XCTUnwrap(memberUsers["user-1"]?["profile_image_placement"])
        var users = try XCTUnwrap(stats["users"] as? [String: [String: Any]])
        users["user-1"]?["profile_image_placement"] = placement
        stats["users"] = users
        return try JSONSerialization.data(withJSONObject: stats)
    }

    private func moderatorStatsFixtureWithoutUser() throws -> Data {
        var stats = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.moderator-stats.default")
        ) as? [String: Any])
        var entries = try XCTUnwrap(stats["stats"] as? [[String: Any]])
        entries[0]["actor_user_id"] = "00000000-0000-7000-8000-000000000499"
        stats["stats"] = entries
        stats["users"] = [String: Any]()
        return try JSONSerialization.data(withJSONObject: stats)
    }
}

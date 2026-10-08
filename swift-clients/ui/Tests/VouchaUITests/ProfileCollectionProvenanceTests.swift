import Foundation
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ProfileCollectionProvenanceTests: NativeRouteSurfaceViewModelTestCase {
    func testFollowedTopicsRenderServerProvenance() async throws {
        try await assertCollection(
            path: "/user/alice/topics/following", endpoint: "/api/v1/users/user-abc/topics/following",
            fixture: "native.users.profile.topics-following.first-page"
        )
    }

    func testMembershipCommunitiesRenderServerProvenance() async throws {
        try await assertCollection(
            path: "/user/alice/communities/member", endpoint: "/api/v1/users/user-abc/communities/member",
            fixture: "native.users.profile.communities-member.first-page"
        )
    }

    func testFollowedSourceCardsRenderServerProvenance() async throws {
        try await assertCollection(
            path: "/user/alice/rss-feeds/following", endpoint: "/api/v1/users/user-abc/rss-feeds/following",
            fixture: "native.users.profile.sources-following.article"
        )
    }

    private func assertCollection(path: String, endpoint: String, fixture: String) async throws {
        let cases: [([String: Any]?, String?)] = [
            (["via": "api", "app": NSNull()], "via API"),
            (["via": "mcp", "app": NSNull()], "via MCP"),
            (["via": "mcp", "app": [
                "kind": "verified",
                "client_id": "private-client",
                "client_name": "Trusted App"
            ]], "via Trusted App"),
            (nil, nil)
        ]
        for (provenance, label) in cases {
            CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
                ApiFixtureLoader.data("native.users.profile.default"), 200
            )
            var response = try XCTUnwrap(
                JSONSerialization.jsonObject(with: ApiFixtureLoader.data(fixture)) as? [String: Any]
            )
            var results = try XCTUnwrap(response["results"] as? [[String: Any]])
            results[0]["provenance"] = provenance
            response["results"] = results
            CannedFeedURLProtocol.handlers[endpoint] = try (JSONSerialization.data(withJSONObject: response), 200)
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: route.entry, client: makeClient(), routeMatch: route.match
            )
            await viewModel.load()
            let surface = NativeUserProfileSurface(
                entry: route.entry, viewModel: viewModel, isSignedIn: true,
                turnstileSiteKey: nil, showSignIn: {}, onNavigate: { _ in }
            )
            let badges = try surface.inspect().findAll(ProvenanceBadge.self)
            if let label {
                let badge = try XCTUnwrap(badges.first)
                XCTAssertEqual(try badge.find(text: label).string(), label)
            } else {
                for badge in badges {
                    XCTAssertTrue(try badge.findAll(ViewType.Text.self).isEmpty)
                }
            }
            XCTAssertThrowsError(try surface.inspect().find(text: "private-client"))
        }
    }
}

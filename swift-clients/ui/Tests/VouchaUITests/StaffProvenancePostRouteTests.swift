import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class StaffProvenancePostRouteTests: NativeRouteSurfaceViewModelTestCase {
    func testRecommendationListAndDetailKeepDecodedPostFacts() async throws {
        let detailPath = "/api/v1/topic-recommendations/recommendation-1"
        let detail = try postFacts(in: ApiFixtureLoader.data("native.topic-recommendation.detail.default"))
        let post = try XCTUnwrap(detail["post"] as? [String: Any])
        let feed: [String: Any] = [
            "results": [["id": "recommendation-1", "entity_id": "recommendation-1"]],
            "posts": ["recommendation-1": post]
        ]
        CannedFeedURLProtocol.handlers["/api/v1/topic-recommendations"] = try (
            JSONSerialization.data(withJSONObject: feed), 200
        )
        CannedFeedURLProtocol.handlers[detailPath] = try (
            JSONSerialization.data(withJSONObject: detail), 200
        )

        let list = try NativeRouteSurfaceViewModel(entry: entry(for: .topicRecommendations), client: nil)
        let listRows = try await list.loadTopicRecommendationRows(client: makeClient())
        try assertStaffCard(listRows)

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/recommendation-1"))
        let focused = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
        let detailRows = try await focused.loadTopicRecommendationRows(client: makeClient())
        try assertStaffCard(detailRows)
    }

    func testCommunityPostsTabKeepsDecodedPostFacts() async throws {
        let response = try postFacts(in: ApiFixtureLoader.data("web.communities.posts.default"), postId: "post-1")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = try (
            JSONSerialization.data(withJSONObject: response), 200
        )
        let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .posts)

        let rows = try await model.loadRows(client: makeClient())

        try assertStaffCard(rows)
    }

    private func assertStaffCard(_ rows: [NativeRouteDestinationRow]) throws {
        let row = try XCTUnwrap(rows.first)
        XCTAssertEqual(row.provenance?.via, "mcp")
        XCTAssertEqual(row.staffProvenance?.createdVia, "mcp")
        XCTAssertThrowsError(try NativeSurfaceRow(row: row).inspect().find(text: "Channel: mcp"))
        XCTAssertNoThrow(try NativeSurfaceRow(row: row, isStaffViewer: true).inspect().find(text: "Channel: mcp"))
    }

    private func postFacts(in data: Data, postId: String? = nil) throws -> [String: Any] {
        var response = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        if let postId {
            var posts = try XCTUnwrap(response["posts"] as? [String: [String: Any]])
            var post = try XCTUnwrap(posts[postId])
            addFacts(to: &post)
            posts[postId] = post
            response["posts"] = posts
        } else {
            var post = try XCTUnwrap(response["post"] as? [String: Any])
            addFacts(to: &post)
            response["post"] = post
        }
        return response
    }

    private func addFacts(to post: inout [String: Any]) {
        post["provenance"] = ["via": "mcp", "app": NSNull()]
        post["staff_provenance"] = ["created_via": "mcp", "oauth_client": NSNull()]
    }
}

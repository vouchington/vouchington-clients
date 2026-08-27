import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteCommunitySurfaceViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityDetailLoadsNativeRows() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/communities/builders": (
                Data(
                    """
                    {
                      "community": {
                        "id": "community-1",
                        "name": "Builders",
                        "description": "Native community"
                      },
                      "community_metrics": {
                        "member_count": 2,
                        "post_count": 1,
                        "list_item_count": 5
                      }
                    }
                    """.utf8
                ),
                200
            ),
            "/api/v1/communities/builders/members": (
                Data(
                    """
                    {
                      "results": [{ "id": "member-1" }],
                      "community_members": {
                        "member-1": {
                          "id": "member-1",
                          "user_id": "user-1",
                          "role": "owner"
                        }
                      },
                      "users": {
                        "user-1": { "id": "user-1", "username": "alice" }
                      }
                    }
                    """.utf8
                ),
                200
            ),
            "/api/v1/communities/builders/posts": (
                Data(
                    """
                    {
                      "results": [{ "id": "post-1" }],
                      "posts": {
                        "post-1": {
                          "id": "post-1",
                          "post_type": "discussion",
                          "title": "Welcome",
                          "created_at": "2026-01-01T00:00:00Z"
                        }
                      }
                    }
                    """.utf8
                ),
                200
            ),
            "/api/v1/communities/builders/list-items/counts": (
                Data(#"{"topic":2,"rss_feed":1,"post":1,"url_hostname":1,"url":0}"#.utf8),
                200
            )
        ]
        CannedFeedURLProtocol.capturedURLs = []
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/builders"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .communityDetail),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.map(\.path).contains("/api/v1/communities/builders"))
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "person.3", title: "Builders", detail: "Native community")
        )
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "chart.bar",
            title: "Analytics",
            detail: "2 members"
        )))
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "list.bullet",
            title: "Lists",
            detail: "5 list items"
        )))
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(icon: "person", title: "alice", detail: "Owner")))
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(icon: "doc.text", title: "Welcome", detail: "Discussion")))
    }
}

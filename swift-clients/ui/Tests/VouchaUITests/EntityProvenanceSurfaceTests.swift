import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class EntityProvenanceSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testListCardsRenderOnlyServerProvidedPublicProvenance() throws {
        let cases: [(String, String?)] = [
            (#","provenance":{"via":"api","app":null}"#, "via API"),
            (#","provenance":{"via":"mcp","app":null}"#, "via MCP"),
            (
                #","provenance":{"via":"mcp","app":{"kind":"verified","client_name":"Example Agent","client_id":"private-id"}}"#,
                "via Example Agent"
            ),
            (#","provenance":{"via":"api","app":{"kind":"hostname","hostname":"example.test"}}"#, "via example.test"),
            (#","provenance":{"via":"mcp","app":{"kind":"known","key":"unreviewed-key"}}"#, "via MCP"),
            ("", nil)
        ]
        for (facts, expected) in cases {
            let json = """
            {"id":"list-1","owner_user_id":"user-1","name":"Reading Queue",
             "visibility":"public","created_at":"2026-06-28T10:00:00Z",
             "updated_at":"2026-06-28T10:00:00Z"\(facts)}
            """
            let decoder = JSONDecoder()
            decoder.keyDecodingStrategy = .convertFromSnakeCase
            decoder.dateDecodingStrategy = .iso8601
            let list = try decoder.decode(UserList.self, from: Data(json.utf8))
            let view = ListsSummaryRow(list: list, isSelected: false)
            let inspection = try view.inspect()
            if let expected {
                XCTAssertNoThrow(try inspection.find(text: expected))
            } else {
                XCTAssertThrowsError(try inspection.find(ProvenanceBadge.self).find(ViewType.Text.self))
            }
            XCTAssertThrowsError(try inspection.find(text: "private-id"))
            XCTAssertThrowsError(try inspection.find(text: "unreviewed-key"))
        }
    }

    func testListsPreviewRetainsFixtureProvenanceThroughRenderedRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (ApiFixtureLoader.data("entity-provenance.lists"), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .lists), client: nil)
        let page = try await viewModel.loadListsPreviewPage(client: makeClient(), after: nil)
        try assertFixtureLabels(page.rows.map(\.row))
    }

    func testTopicBrowseRetainsFixtureProvenanceThroughRenderedRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (ApiFixtureLoader.data("entity-provenance.topics"), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .topicsBrowse), client: nil)
        let rows = try await viewModel.loadEntityRows(for: .topicsBrowse, client: makeClient())
        try assertFixtureLabels(rows)
    }

    func testSourceBrowseRetainsFixtureProvenanceThroughRenderedRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            ApiFixtureLoader.data("entity-provenance.rss-feeds"),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/news-sources"))
        let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
        let rows = try await viewModel.loadSourceRows(client: makeClient())
        try assertFixtureLabels(rows)
    }

    func testCommunityBrowseRetainsFixtureProvenanceThroughRenderedRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (
            ApiFixtureLoader.data("entity-provenance.communities"),
            200
        )
        let viewModel = try CommunityBrowseViewModel(client: makeClient())
        await viewModel.load()
        XCTAssertEqual(viewModel.rows.count, 3)
        let rows = try CommunityBrowseSurface(viewModel: viewModel).inspect().findAll(NativeSurfaceRow.self)
        XCTAssertEqual(rows.count, 3)
        try assertFixtureLabels(rows.map { try $0.actualView().row })
    }

    func testCommunityDiscoveryRetainsFixtureProvenanceThroughRenderedRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (
            ApiFixtureLoader.data("entity-provenance.communities"),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .communitiesBrowse), client: nil)
        let rows = try await viewModel.loadDiscoveryRows(for: .communitiesBrowse, client: makeClient())
        try assertFixtureLabels(rows)
    }

    func testCommunityDetailHeaderRendersFixtureProvenance() throws {
        let page = try fixtureObject("entity-provenance.communities")
        let communities = try XCTUnwrap(page["communities"] as? [String: Any])
        for (suffix, expected) in [("mcp", "via Fixture Agent"), ("api", "via API"), ("web", nil)] {
            let community = try XCTUnwrap(communities["community-1-" + suffix])
            let data = try JSONSerialization.data(withJSONObject: ["community": community])
            let decoder = JSONDecoder()
            decoder.keyDecodingStrategy = .convertFromSnakeCase
            decoder.dateDecodingStrategy = .iso8601
            let viewModel = CommunityDetailViewModel(client: nil, slug: "fixture")
            viewModel.communityDetail = try decoder.decode(CommunityResponse.self, from: data)
            let inspection = try CommunityWorkspaceSurface(viewModel: viewModel).inspect()
            if let expected {
                XCTAssertNoThrow(try inspection.find(text: expected))
            } else {
                XCTAssertThrowsError(try inspection.find(ProvenanceBadge.self).find(ViewType.Text.self))
            }
        }
    }

    func testTopicDetailHeaderRetainsFixtureProvenance() async throws {
        let page = try fixtureObject("entity-provenance.topics")
        let topics = try XCTUnwrap(page["topics"] as? [String: Any])
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/fixture"))
        var headers: [NativeRouteDestinationRow] = []
        for suffix in ["mcp", "api", "web"] {
            let topic = try XCTUnwrap(topics.values.first { value in
                (value as? [String: Any])?["id"] as? String == "topic-1-" + suffix
            })
            CannedFeedURLProtocol.handlers["/api/v1/topics/fixture"] = try (
                JSONSerialization.data(withJSONObject: ["topic": topic]), 200
            )
            let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
            let rows = try await viewModel.loadEntityRows(for: .topicDetail, client: makeClient())
            try headers.append(XCTUnwrap(rows.first))
        }
        try assertFixtureLabels(headers)
    }

    func testSourceDetailHeaderRetainsFixtureProvenance() async throws {
        var page = try fixtureObject("entity-provenance.rss-feeds")
        let feeds = try XCTUnwrap(page["results"] as? [[String: Any]])
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/fixture"))
        var headers: [NativeRouteDestinationRow] = []
        for feed in feeds {
            page["results"] = [feed]
            CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = try (
                JSONSerialization.data(withJSONObject: page), 200
            )
            let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
            let rows = try await viewModel.loadSourceRows(client: makeClient())
            try headers.append(XCTUnwrap(rows.first))
        }
        try assertFixtureLabels(headers)
    }

    private func fixtureObject(_ id: String) throws -> [String: Any] {
        try XCTUnwrap(JSONSerialization.jsonObject(with: ApiFixtureLoader.data(id)) as? [String: Any])
    }

    private func assertFixtureLabels(_ rows: [NativeRouteDestinationRow]) throws {
        XCTAssertEqual(rows.count, 3)
        guard rows.count == 3 else { return }
        for (row, expected) in zip(rows, ["via Fixture Agent", "via API", nil]) {
            let inspection = try NativeSurfaceRow(row: row).inspect()
            if let expected {
                XCTAssertNoThrow(try inspection.find(text: expected))
            } else {
                XCTAssertThrowsError(try inspection.find(ProvenanceBadge.self).find(ViewType.Text.self))
            }
            XCTAssertThrowsError(try inspection.find(text: "voucha_fixture_agent"))
        }
    }

}

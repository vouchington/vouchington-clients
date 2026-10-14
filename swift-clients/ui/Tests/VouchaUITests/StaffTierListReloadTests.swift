import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class StaffTierListReloadTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    func testRetainedSourcesModelRefetchesWhenViewerGainsStaffAccess() async throws {
        let path = "/api/v1/rss-feeds"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (ApiFixtureLoader.data("entity-provenance.rss-feeds"), 200, 0),
            (ApiFixtureLoader.data("entity-provenance.rss-feeds.staff"), 200, 0)
        ]
        let model = SourcesListViewModel(client: makeClient(), userId: nil, feedType: nil)
        let publicView = SourcesListView(viewModel: model, scope: .all, navigationTitle: "Sources")
        try await publicView.inspect().group().callTask(id: "all|false")
        XCTAssertNil(model.allSources.first?.staffProvenance)

        let promotedView = SourcesListView(
            viewModel: model,
            scope: .all,
            navigationTitle: "Sources",
            isStaffViewer: true
        )
        try await promotedView.inspect().group().callTask(id: "all|true")
        XCTAssertNotNil(model.allSources.first?.staffProvenance)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.count, 2)
    }

    func testRetainedCommunityBrowseModelRefetchesWhenViewerGainsStaffAccess() async throws {
        let path = "/api/v1/communities"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (ApiFixtureLoader.data("entity-provenance.communities"), 200, 0),
            (ApiFixtureLoader.data("entity-provenance.communities.staff"), 200, 0)
        ]
        let model = CommunityBrowseViewModel(client: makeClient())
        let publicView = CommunityBrowseSurface(viewModel: model)
        try await publicView.inspect().vStack().callTask(id: false)
        XCTAssertNil(model.rows.first?.staffProvenance)

        let promotedView = CommunityBrowseSurface(viewModel: model, isStaffViewer: true)
        try await promotedView.inspect().vStack().callTask(id: true)
        XCTAssertNotNil(model.rows.first?.staffProvenance)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.count, 2)
    }

    func testRetainedListsModelRefetchesWhenViewerGainsStaffAccess() async throws {
        let path = "/api/v1/lists"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (ApiFixtureLoader.data("entity-provenance.lists"), 200, 0),
            (ApiFixtureLoader.data("entity-provenance.lists.staff"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1-mcp/items"] = (
            ApiFixtureLoader.data("native.list-items.default"), 200
        )
        let model = ListsViewModel(client: makeClient())
        let publicView = ListsView(viewModel: model)
        try await publicView.inspect().scrollView().callTask(id: false)
        XCTAssertNil(model.selectedList?.staffProvenance)

        let promotedView = ListsView(viewModel: model, isStaffViewer: true)
        try await promotedView.inspect().scrollView().callTask(id: true)
        XCTAssertNotNil(model.selectedList?.staffProvenance)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.count, 2)
    }

    func testRetainedPostsModelRefetchesWhenViewerGainsStaffAccess() async throws {
        let path = "/api/v1/feeds/posts/any"
        let staffPage = try feedPage(from: ApiFixtureLoader.data("native.users.profile.posts.staff-provenance"))
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (removingStaffFacts(from: staffPage), 200, 0),
            (staffPage, 200, 0)
        ]
        let model = PostsListViewModel(client: makeClient())
        let publicView = PostsListView(viewModel: model)
        try await publicView.inspect().group().callTask(id: false)
        XCTAssertFalse(model.items.isEmpty, "initial state=\(model.state)")
        XCTAssertNil(model.items.first(where: { $0.id == "profile-mcp-1" })?.staffProvenance)

        let promotedView = PostsListView(viewModel: model, isStaffViewer: true)
        try await promotedView.inspect().group().callTask(id: true)
        XCTAssertNotNil(
            model.items.first(where: { $0.id == "profile-mcp-1" })?.staffProvenance,
            "items=\(model.items.map { "\($0.id):\($0.staffProvenance?.createdVia ?? "none")" }), requests=\(CannedFeedURLProtocol.capturedURLs.map(\.path))"
        )
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.count, 2)
    }

    private func makeClient() -> APIClient {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        return APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func removingStaffFacts(from data: Data) throws -> Data {
        var page = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var posts = try XCTUnwrap(page["posts"] as? [String: [String: Any]])
        for key in posts.keys {
            posts[key]?.removeValue(forKey: "staff_provenance")
        }
        page["posts"] = posts
        return try JSONSerialization.data(withJSONObject: page)
    }

    private func feedPage(from data: Data) throws -> Data {
        var page = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let results = try XCTUnwrap(page["results"] as? [[String: Any]])
        page["results"] = try results.map { try ["entity_id": XCTUnwrap($0["id"] as? String)] }
        return try JSONSerialization.data(withJSONObject: page)
    }
}

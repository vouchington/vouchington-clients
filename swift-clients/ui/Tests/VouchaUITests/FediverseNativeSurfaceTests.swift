import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FediverseNativeSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testDeepLinkRestoresEveryProviderAndSerializesMatchingRequest() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/search"] = (
            Data(#"{"buckets":[]}"#.utf8), 200
        )

        for provider in FediverseProviderFilter.allCases {
            CannedFeedURLProtocol.capturedURLs = []
            let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
                for: "/fediverse?provider=\(provider.rawValue)&q=native"
            )?.match)
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: entry(for: .fediverseSearch),
                client: makeClient(),
                routeMatch: match
            )

            await viewModel.search(query: "native")

            XCTAssertEqual(viewModel.fediverseProvider, provider)
            let expectedQuery = provider == .all
                ? "q=native&limit=10"
                : "q=native&providers=\(provider.rawValue)&limit=10"
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, expectedQuery)
            XCTAssertEqual(
                viewModel.fediverseRoute(query: "native"),
                provider == .all
                    ? "/fediverse?q=native"
                    : "/fediverse?q=native&provider=\(provider.rawValue)"
            )
        }
    }

    func testNewProviderSearchSupersedesCancelledStaleResponse() async throws {
        let path = "/api/v1/fediverse/search"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(#"{"buckets":[{"provider":"mastodon","status":"error","items":[]}] }"#.utf8), 200, 0.2),
            (Data(#"{"buckets":[{"provider":"bluesky","status":"error","items":[]}] }"#.utf8), 200, 0)
        ]
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .fediverseSearch), client: makeClient()
        )
        viewModel.fediverseProvider = .mastodon
        let staleSearch = Task { await viewModel.search(query: "native") }
        try await waitForCapturedPath(path)

        viewModel.fediverseProvider = .bluesky
        staleSearch.cancel()
        await viewModel.search(query: "native")
        await staleSearch.value

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "q=native&providers=bluesky&limit=10")
        XCTAssertEqual(viewModel.searchSections.map { uiEnglish($0.title) }, ["Bluesky"])
        guard case .loaded = viewModel.state else {
            return XCTFail("Expected the current provider response to finish loading")
        }
    }

    func testInvalidProviderFallsBackToAllAndCanonicalRoutePreservesQuery() throws {
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/fediverse?provider=invalid")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .fediverseSearch), client: makeClient(), routeMatch: match
        )
        XCTAssertEqual(viewModel.fediverseProvider, .all)
        viewModel.fediverseProvider = .mastodon
        XCTAssertEqual(
            viewModel.fediverseRoute(query: "  native swift  "),
            "/fediverse?q=native%20swift&provider=mastodon"
        )
    }

    func testProviderSelectionRoutesWithoutDispatchingACompetingRequest() async throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .fediverseSearch), client: makeClient())
        var route: String?
        let surface = NativeSearchSurface(viewModel: viewModel) { route = $0 }

        try surface.inspect().find(button: "Mastodon").tap()

        XCTAssertEqual(route, "/fediverse?provider=mastodon")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/search"] = (
            Data(#"{"buckets":[]}"#.utf8), 200
        )
        await viewModel.search(query: "native")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
    }

    func testDirectoryAppendsCursorPageAndDeduplicatesStableIds() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/fediverse/instances"] = [
            (ApiFixtureLoader.data("native.fediverse.instances.default"), 200, 0),
            (ApiFixtureLoader.data("native.fediverse.instances.page-2"), 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
            for: "/instances?q=native&sort=new"
        ))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        await viewModel.loadMoreFediverseInstances()
        XCTAssertEqual(Set(viewModel.fediverseInstanceItems.map(\.id)).count, viewModel.fediverseInstanceItems.count)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.query), [
            "limit=25&q=native&sort=new",
            "limit=25&after=next&q=native&sort=new"
        ])
    }

    func testDirectoryDeduplicatesStableIdsOnFirstPageLoad() async throws {
        var object = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.fediverse.instances.default")
            ) as? [String: Any]
        )
        var results = try XCTUnwrap(object["results"] as? [[String: Any]])
        try results.append(XCTUnwrap(results.first))
        object["results"] = results
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/instances"] = try (
            JSONSerialization.data(withJSONObject: object), 200
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .fediverseInstances), client: makeClient()
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.fediverseInstanceItems.count, 2)
        XCTAssertEqual(Set(viewModel.fediverseInstanceItems.map(\.id)).count, 2)
    }

    func testIncomingPageDeduplicatesRepeatedIdsIncrementally() throws {
        let response = try APIClient.makeDecoder().decode(
            FediverseInstancesResponse.self,
            from: ApiFixtureLoader.data("native.fediverse.instances.page-2")
        )
        let item = try XCTUnwrap(response.orderedInstances.first)
        XCTAssertEqual(NativeRouteSurfaceViewModel.uniqueFediverseInstances([item, item], excluding: []).count, 1)
    }

    func testTrustTierMatchesWebThresholds() {
        XCTAssertEqual(FediverseTrustTier(election: nil), .unrated)
        XCTAssertEqual(FediverseTrustTier(election: election(score: 3, up: 5, down: 2)), .trusted)
        XCTAssertEqual(FediverseTrustTier(election: election(score: 3, up: 4, down: 1)), .neutral)
        XCTAssertEqual(FediverseTrustTier(election: election(score: -3, up: 1, down: 4)), .distrusted)
        XCTAssertEqual(FediverseTrustTier(election: election(score: 0, up: 0, down: 0)), .unrated)
    }

    private func election(score: Double, up: Int, down: Int) -> HostnameElection {
        HostnameElection(id: "hostname", votesScoreNet: score, votesCountUp: up, votesCountDown: down)
    }

    private func waitForCapturedPath(_ path: String) async throws {
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.hasCapturedRequest(path: path) {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for captured path \(path)")
    }

    func testInstanceSlugDetailUsesDedicatedEndpointAndKeepsTopicActions() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/instances/social-example"] = (
            ApiFixtureLoader.data("native.fediverse.instance.slug"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/00000000-0000-7000-8000-00000000f001"] = (
            Data(#"{"bookmarks":{}}"#.utf8), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/instance/social-example"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.topicDetailId, "00000000-0000-7000-8000-00000000f001")
        XCTAssertEqual(viewModel.rows.map(\.title), [
            "social.example", "Software", "Protocol", "Users", "Active this month", "Registrations", "Trust"
        ])
        XCTAssertEqual(viewModel.detailRelationEntityType, "topic")
    }

    func testInstanceDetailRendersSafeFallbackWhenExtensionIsNull() async throws {
        var object = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.fediverse.instance.slug")
            ) as? [String: Any]
        )
        object["fediverse_instance"] = NSNull()
        CannedFeedURLProtocol.handlers["/api/v1/fediverse/instances/social-example"] = try (
            JSONSerialization.data(withJSONObject: object), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/00000000-0000-7000-8000-00000000f001"] = (
            Data(#"{"bookmarks":{}}"#.utf8), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/instance/social-example"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.rows.dropFirst().map(\.detail), [
            "Unclassified", "Unknown", "Unknown", "Unknown", "Unknown", "Trusted"
        ])
    }
}

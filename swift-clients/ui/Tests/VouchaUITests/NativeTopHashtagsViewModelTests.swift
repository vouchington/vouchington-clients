import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeTopHashtagsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testAuthoredHashtagTokenDoesNotDuplicateLeadingHash() {
        XCTAssertEqual(nativeAuthoredHashtagToken("swift"), "#swift")
        XCTAssertEqual(nativeAuthoredHashtagToken("#swift"), "#swift")
    }

    func testRootSurfaceRendersRecommendationsAndTopHashtagsTabs() async throws {
        let entry = try entry(for: .topicRecommendations)
        let routeViewModel = NativeRouteSurfaceViewModel(entry: entry, client: nil)
        let surface = NativeTopicRecommendationsRootSurface(
            routeViewModel: routeViewModel,
            isSignedIn: true,
            isAdministrator: false,
            onNavigateToTargetPath: { _ in }
        )

        try await ViewHosting.host(surface) {
            XCTAssertNoThrow(try surface.inspect().find(text: "Recommendations"))
            XCTAssertNoThrow(try surface.inspect().find(text: "Top Hashtags"))
        }
    }

    func testRootSurfaceShowsSignInActionForSignedOutHashtagBrowsing() async throws {
        let entry = try entry(for: .topicRecommendations)
        let routeViewModel = NativeRouteSurfaceViewModel(entry: entry, client: nil)
        var navigatedPath: String?
        let surface = NativeTopicRecommendationsRootSurface(
            routeViewModel: routeViewModel,
            isSignedIn: false,
            isAdministrator: false,
            initialTab: .hashtags,
            onNavigateToTargetPath: { navigatedPath = $0 }
        )

        try await ViewHosting.host(surface) {
            try surface.inspect().find(button: "Sign In").tap()

            XCTAssertEqual(navigatedPath, "/login")
        }
    }

    func testRootSurfaceRendersSignedInHashtagFilters() async throws {
        let entry = try entry(for: .topicRecommendations)
        let routeViewModel = NativeRouteSurfaceViewModel(entry: entry, client: nil)
        let surface = NativeTopicRecommendationsRootSurface(
            routeViewModel: routeViewModel,
            isSignedIn: true,
            isAdministrator: true,
            initialTab: .hashtags,
            onNavigateToTargetPath: { _ in }
        )

        try await ViewHosting.host(surface) {
            try surface.inspect().find(ViewType.Picker.self) { picker in
                (try? picker.selectedValue(TopHashtagMapping.self)) == .all
            }
            .select(value: TopHashtagMapping.unlinked)
            try surface.inspect().find(ViewType.TextField.self).setInput("swift")

            XCTAssertNoThrow(try surface.inspect().find(button: "Search").tap())
            XCTAssertNoThrow(try surface.inspect().find(text: "Unlinked"))
        }
    }

    func testReloadFiltersAndAppendsTopHashtags() async throws {
        let firstPage = topHashtagsData(id: "alias-1", hasNextPage: true, endCursor: "cursor-1")
        let decoded = try JSONDecoder.vouchaFixtureDecoder.decode(TopHashtagsResponse.self, from: firstPage)
        XCTAssertEqual(decoded.results.map(\.id), ["alias-1"])
        CannedFeedURLProtocol.queuedHandlers["/api/v1/topic-recommendations/top-hashtags"] = [
            (firstPage, 200, 0),
            (topHashtagsData(id: "alias-2", hasNextPage: false, endCursor: nil), 200, 0)
        ]
        let viewModel = try NativeTopHashtagsViewModel(client: makeClient())
        viewModel.query = "swift_dev"
        viewModel.mapping = .unlinked

        await viewModel.reload()
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.results.map(\.id), ["alias-1", "alias-2"])
        XCTAssertEqual(viewModel.topics["topic-1"]?.slug, "swift-ui")
        XCTAssertFalse(viewModel.pageInfo.hasNextPage)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.last.flatMap {
                URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems?
                    .first(where: { $0.name == "after" })?.value
            },
            "cursor-1"
        )
    }

    func testLoadMoreUsesTheQueryAndMappingThatProducedItsCursor() async throws {
        let path = "/api/v1/topic-recommendations/top-hashtags"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (topHashtagsData(id: "first", hasNextPage: true, endCursor: "next"), 200, 0),
            (topHashtagsData(id: "second"), 200, 0)
        ]
        let viewModel = try NativeTopHashtagsViewModel(client: makeClient())
        viewModel.query = "swift"
        viewModel.mapping = .unlinked

        await viewModel.reload()
        viewModel.query = "rust"
        viewModel.mapping = .linked
        await viewModel.loadMore()

        let latestURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let queryItems = try XCTUnwrap(
            URLComponents(url: latestURL, resolvingAgainstBaseURL: false)?.queryItems
        )
        XCTAssertEqual(queryItems.first(where: { $0.name == "q" })?.value, "swift")
        XCTAssertEqual(queryItems.first(where: { $0.name == "mapping" })?.value, "unlinked")
        XCTAssertEqual(queryItems.first(where: { $0.name == "after" })?.value, "next")
    }

    func testNewerQueryAndMappingSupersedeAnInitialLoad() async throws {
        let path = "/api/v1/topic-recommendations/top-hashtags"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (topHashtagsData(id: "stale"), 200, 0),
            (topHashtagsData(id: "current"), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        let viewModel = try NativeTopHashtagsViewModel(client: makeClient())
        viewModel.query = "stale"
        let staleRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let staleLoad = Task { await viewModel.reload() }
        _ = try await staleRequest.wait()

        viewModel.query = "current"
        viewModel.mapping = .unlinked
        let currentRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let currentLoad = Task { await viewModel.reload() }
        let request = try await currentRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await currentLoad.value
        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleLoad.value

        XCTAssertEqual(viewModel.results.map(\.id), ["current"])
        XCTAssertNil(viewModel.error)
        XCTAssertFalse(viewModel.isLoading)
        let query = URLComponents(url: request.url, resolvingAgainstBaseURL: false)?.queryItems
        XCTAssertEqual(query?.first(where: { $0.name == "q" })?.value, "current")
        XCTAssertEqual(query?.first(where: { $0.name == "mapping" })?.value, "unlinked")
    }

    func testNewerQueryAndMappingSupersedeLoadMore() async throws {
        let path = "/api/v1/topic-recommendations/top-hashtags"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (topHashtagsData(id: "initial", hasNextPage: true, endCursor: "next"), 200, 0),
            (topHashtagsData(id: "stale"), 200, 0),
            (topHashtagsData(id: "current"), 200, 0)
        ]
        let viewModel = try NativeTopHashtagsViewModel(client: makeClient())
        await viewModel.reload()
        CannedFeedURLProtocol.suspendResponse(path: path)
        let staleRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let staleLoad = Task { await viewModel.loadMore() }
        _ = try await staleRequest.wait()

        viewModel.query = "current"
        viewModel.mapping = .linked
        let currentRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let currentLoad = Task { await viewModel.reload() }
        let request = try await currentRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await currentLoad.value
        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleLoad.value

        XCTAssertEqual(viewModel.results.map(\.id), ["current"])
        XCTAssertNil(viewModel.error)
        XCTAssertFalse(viewModel.isLoading)
        let query = URLComponents(url: request.url, resolvingAgainstBaseURL: false)?.queryItems
        XCTAssertEqual(query?.first(where: { $0.name == "q" })?.value, "current")
        XCTAssertEqual(query?.first(where: { $0.name == "mapping" })?.value, "linked")
    }

    func testAdminMutationsLinkUnlinkAndCreateTopicFromAlias() async throws {
        let path = "/api/v1/topic-recommendations/top-hashtags"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (topHashtagsData(id: "alias-1", topicId: nil), 200, 0),
            (topHashtagsData(id: "alias-1", topicId: "topic-1"), 200, 0),
            (topHashtagsData(id: "alias-1", topicId: nil), 200, 0),
            (topHashtagsData(id: "alias-1", topicId: "topic-2"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/aliases/alias-1"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/aliases/alias-1"] = (Data("{}".utf8), 204)
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            Data(
                #"""
                {"topic":{"id":"topic-2","name":"Swift UI","slug":"swift-ui",
                "markdown":null,"topic_type":"topic","hostname_id":null,"hostname":null,
                "logo_image_id":null,"hero_image_id":null,"homepage_url_id":null,
                "lingua_rs_detected_language":null,"referral_program_id":null,
                "referral_program_slug":null,"rewards_program_id":null,
                "created_at":"2026-01-01T00:00:00.000Z"}}
                """#
                .utf8
            ),
            201
        )
        let viewModel = try NativeTopHashtagsViewModel(client: makeClient())

        await viewModel.reload()
        let alias = try XCTUnwrap(viewModel.results.first)
        await viewModel.link(alias: alias, to: "topic-1")
        try await viewModel.unlink(alias: XCTUnwrap(viewModel.results.first))
        await viewModel.createTopic(alias: alias, name: "Swift UI")

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/topics/topic-1/aliases/alias-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/topics" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains {
            $0?.contains("source_topic_alias_id") == true
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains {
            $0?.contains(#""slug":"swift-ui""#) == true
        })
    }

    func testAdminHashtagActionsAreDisabledWhileLoading() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            TopHashtagsResponse.self,
            from: topHashtagsData(id: "alias-1", topicId: nil)
        )
        let row = try NativeTopHashtagRow(
            hashtag: XCTUnwrap(response.results.first),
            topic: nil,
            isAdministrator: true,
            isLoading: true,
            locale: Locale(identifier: "en"),
            link: { _ in },
            unlink: {},
            create: { _ in }
        )

        XCTAssertTrue(try row.inspect().find(button: "Link Topic").isDisabled())
        XCTAssertTrue(try row.inspect().find(button: "Create Topic").isDisabled())
    }

    func testAdminCannotUnlinkTheActiveTopicSlug() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            TopHashtagsResponse.self,
            from: topHashtagsData(id: "alias-1", hashtag: "#Swift_UI")
        )
        let protectedRow = try NativeTopHashtagRow(
            hashtag: XCTUnwrap(response.results.first),
            topic: XCTUnwrap(response.topics["topic-1"]),
            isAdministrator: true,
            isLoading: false,
            locale: Locale(identifier: "en"),
            link: { _ in },
            unlink: {},
            create: { _ in }
        )
        let missingSidecarRow = try NativeTopHashtagRow(
            hashtag: XCTUnwrap(response.results.first),
            topic: nil,
            isAdministrator: true,
            isLoading: false,
            locale: Locale(identifier: "en"),
            link: { _ in },
            unlink: {},
            create: { _ in }
        )

        XCTAssertThrowsError(try protectedRow.inspect().find(button: "Unlink"))
        XCTAssertNoThrow(try missingSidecarRow.inspect().find(button: "Unlink"))
    }

    func testNoClientAndInvalidMutationsLeaveExistingResultsUntouched() async throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            TopHashtagsResponse.self,
            from: topHashtagsData(id: "alias-1", topicId: nil, hasNextPage: true, endCursor: "next")
        )
        let viewModel = NativeTopHashtagsViewModel(client: nil)
        viewModel.results = response.results
        viewModel.pageInfo = response.pageInfo
        let alias = try XCTUnwrap(response.results.first)

        await viewModel.reload()
        await viewModel.loadMore()
        await viewModel.link(alias: alias, to: " ")
        await viewModel.unlink(alias: alias)
        await viewModel.createTopic(alias: alias, name: " ")

        XCTAssertEqual(viewModel.results.map(\.id), ["alias-1"])
        XCTAssertFalse(viewModel.isLoading)
        XCTAssertNil(viewModel.error)
    }

    func testReloadAndMutationSurfaceServerErrors() async throws {
        let listPath = "/api/v1/topic-recommendations/top-hashtags"
        CannedFeedURLProtocol.handlers[listPath] = (Data("{}".utf8), 500)
        let viewModel = try NativeTopHashtagsViewModel(client: makeClient())

        await viewModel.reload()

        XCTAssertNotNil(viewModel.error)
        XCTAssertFalse(viewModel.isLoading)

        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            TopHashtagsResponse.self,
            from: topHashtagsData(id: "alias-1")
        )
        let alias = try XCTUnwrap(response.results.first)
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/aliases/alias-1"] = (Data("{}".utf8), 500)

        await viewModel.link(alias: alias, to: "topic-1")

        XCTAssertNotNil(viewModel.error)
        XCTAssertFalse(viewModel.isLoading)
    }
}

private func topHashtagsData(
    id: String,
    topicId: String? = "topic-1",
    hashtag: String = "swift-ui",
    hasNextPage: Bool = false,
    endCursor: String? = nil
) -> Data {
    let topics: [String: Any] = topicId == nil ? [:] : ["topic-1": [
        "id": "topic-1", "name": "SwiftUI", "slug": "swift-ui", "markdown": NSNull(),
        "topic_type": "topic", "hostname_id": NSNull(), "hostname": NSNull(),
        "logo_image_id": NSNull(), "hero_image_id": NSNull(), "homepage_url_id": NSNull(),
        "lingua_rs_detected_language": NSNull(), "referral_program_id": NSNull(),
        "referral_program_slug": NSNull(), "rewards_program_id": NSNull(),
        "created_at": "2026-01-01T00:00:00.000Z"
    ]]
    let topicIdValue: Any = topicId.map { $0 as Any } ?? NSNull()
    let endCursorValue: Any = endCursor.map { $0 as Any } ?? NSNull()
    let payload: [String: Any] = [
        "results": [[
            "topic_alias_id": id, "hashtag": hashtag, "item_count": 4,
            "contributor_count": 3, "latest_content_id": "content-1",
            "topic_id": topicIdValue
        ]],
        "page_info": [
            "has_next_page": hasNextPage,
            "end_cursor": endCursorValue,
            "start_cursor": NSNull()
        ],
        "topics": topics
    ]
    guard let data = try? JSONSerialization.data(withJSONObject: payload) else {
        preconditionFailure("Top-hashtag test payload must be valid JSON")
    }
    return data
}
